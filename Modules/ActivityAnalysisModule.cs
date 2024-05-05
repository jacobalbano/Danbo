using Discord.Interactions;
using Discord;
using System.Text;
using Danbo.Apis;
using Danbo.Services;
using Danbo.Models.Jobs;
using Danbo.Modules.Autocompletion;
using Danbo.Errors;

namespace Danbo.Modules;

[Group("activity-analysis", "Server activity analysis")]
[RequireOwner, DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class ActivityAnalysisModule : ModuleBase
{
    [SlashCommand("show-blacklist", "Display the list of blacklisted channels")]
    public async Task ShowBlacklist()
    {
        await DeferAsync();
        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription(string.Join("\n", analysisApi.GetBlacklist()
            .Select(x => $"- {MentionUtils.MentionChannel(x.ChannelId)}"))
            .NullIfEmpty() ?? "No channels blacklisted")
            .Build());
    }

    [SlashCommand("add-blacklist", "Add a channel to the blacklist")]
    public async Task AddBlacklist(ITextChannel channel)
    {
        await DeferAsync();

        analysisApi.AddBlacklist(channel);
        audit.Log("Added channel to analysis blacklist", userId: Context.User.Id, detailId: channel.Id, detailIdType: Models.DetailIdType.Channel);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription($"Added {channel.Mention}")
            .Build());
    }

    [SlashCommand("remove-blacklist", "Remove a channel from the blacklist")]
    public async Task RemoveBlacklist(ITextChannel channel)
    {
        await DeferAsync();

        analysisApi.RemoveBlacklist(channel);
        audit.Log("Removed channel from analysis blacklist", userId: Context.User.Id, detailId: channel.Id, detailIdType: Models.DetailIdType.Channel);
        await FollowupAsync(embed: new EmbedBuilder()
            .WithDescription($"Removed {channel.Mention}")
            .Build());
    }

    [SlashCommand("start", "Download all messages in the history of the server for analysis")]
    public async Task AnalyzeActivity(
        [Summary(description: "The key of the job to resume"), Autocomplete(typeof(AnalysisJobAutocomplete))] string analysisKey = null
    )
    {
        await DeferAsync();
        var ack = await FollowupAsync("Analysis started, please wait");
        audit.Log("Started analysis job", userId: Context.User.Id, detailMessage: analysisKey);

        if (!Guid.TryParse(analysisKey, out var key))
            key = Guid.NewGuid();

        IReadOnlyList<ChannelReport> lastProgress = Array.Empty<ChannelReport>();
        var message = await ack.ReplyAsync(MakeReport(lastProgress));

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            int delay = 0;
            var rqo = new RequestOptions()
            {
                RatelimitCallback = info => { delay = (info.RetryAfter ?? 5) + 1; return Task.CompletedTask; },
            };

            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1 + delay));
                if (delay > 0) --delay;
                try { await message.ModifyAsync(x => x.Content = MakeReport(lastProgress), rqo); }
                catch { }
            }
        });

        var result = await analysisApi.Run(key, new Progress<IReadOnlyList<ChannelReport>>(x => lastProgress = x));
        cts.Cancel();
        await Retry(() => message.ModifyAsync(x => x.Content = MakeReport(result.Channels)));

        switch (result.State)
        {
            case AnalysisState.Done:
                var bytes = Encoding.UTF8.GetBytes(MakeDetailedReport(result));
                using (var stream = new MemoryStream(bytes))
                    await Context.Channel.SendFileAsync(stream, "activityAnalysis.txt", messageReference: message.Reference);
                break;
            case AnalysisState.Paused:
                await Retry(() => message.ReplyAsync("Analysis paused"));
                return;
            default:
                throw UnhandledEnumException.From(result.State);
        }
    }

    private static async Task<T> Retry<T>(Func<Task<T>> method)
    {
        int attempts = 0;
        while (true)
        {
            try { return await method(); }
            catch
            {
                if (++attempts > 5) throw;
                await Task.Delay(TimeSpan.FromSeconds(attempts));
            }
        }
    }

    private static async Task Retry(Func<Task> method)
    {
        int attempts = 0;
        while (true)
        {
            try { await method(); break; }
            catch
            {
                if (++attempts > 5) throw;
                await Task.Delay(TimeSpan.FromSeconds(attempts));
            }
        }
    }

    [SlashCommand("pause", "Pause an ongoing analysis job")]
    public async Task StopAnalysis(
        [Summary(description: "The key of the job to stop"), Autocomplete(typeof(AnalysisJobAutocomplete))] string analysisKey
    )
    {
        await DeferAsync(ephemeral: true);

        if (!Guid.TryParse(analysisKey, out var key))
            throw new Exception($"Bad key parameter: {analysisKey}");

        audit.Log("Canceled analysis job", userId: Context.User.Id, detailMessage: key.ToString());
        analysisApi.Cancel(key);

        await FollowupAsync("Analysis paused", ephemeral: true);
    }

    [SlashCommand("cleanup", "Remove all past analysis jobs")]
    public async Task Cleanup()
    {
        await DeferAsync();
        analysisApi.Cleanup();

        await FollowupAsync("Analysis jobs cleaned up");
    }

    static string MakeReport(IReadOnlyList<ChannelReport> channels)
    {
        var sb = new StringBuilder();

        var doneChannels = channels
            .Where(x => x.State == AnalysisState.Done)
            .ToList();

        var errorChannels = channels
            .Where(x => x.State == AnalysisState.Error)
            .ToList();

        var skip = channels.FindIndex(x => x.State == AnalysisState.Running);
        if (skip <= 0)
            skip = channels.Count - 3;
        var slice = channels.Skip(skip - 3)
            .Take(6)
            .ToList();

        var remaining = channels.Count - slice.Count;
        sb.AppendLine($"**Messages processed**: {channels.Select(x => (decimal)x.ProcessedMessages).Sum()}");
        sb.AppendLine($"**Channels processed** ({doneChannels.Count}/{channels.Count}):");
        foreach (var c in slice)
            sb.AppendLine($"- {c.State.ToEmoji()} {MentionUtils.MentionChannel(c.ChannelId)} ({c.ProcessedMessages})");
        if (remaining > 0)
            sb.AppendLine($"- ...and {remaining} more");

        if (errorChannels.Any())
        {
            sb.AppendLine("**Error channels**:");
            foreach (var c in errorChannels.Take(6))
                sb.AppendLine($"- {c.State.ToEmoji()} {MentionUtils.MentionChannel(c.ChannelId)}");
            var remainingErrors = errorChannels.Count - 6;
            if (remainingErrors > 0)
                sb.AppendLine($"- ...and {remainingErrors} more");
        }

        return sb.ToString();
    }

    private string MakeDetailedReport(GuildReport result)
    {
        var data = result.Users
            .Where(x => x.GuildJoinInstant != null)
            .OrderBy(x => x.MessageCount)
            .ThenBy(x => x.MessagesEdited)
            .ThenBy(x => x.GuildJoinInstant);

        var sb = new StringBuilder();
        var fields = new object[5] { "Id", "Mention", "JoinDate", "MessageCount", "MessagesEdited" };
        var record = () => sb.AppendLine(string.Join('|', fields));
        record();

        foreach (var user in data)
        {
            fields[0] = user.UserId;
            fields[1] = MentionUtils.MentionUser(user.UserId);
            fields[2] = user.GuildJoinInstant.Value
                .InZone(timezoneProvider.Tzdb.GetSystemDefault())
                .ToString("uuuu-MM-dd ", null);
            fields[3] = user.MessageCount;
            fields[4] = user.MessagesEdited;
            record();
        }

        return sb.ToString();
    }

    public ActivityAnalysisModule(AuditApi audit, AnalysisApi analysisApi, TimezoneProviderService timezoneProvider)
    {
        this.audit = audit;
        this.analysisApi = analysisApi;
        this.timezoneProvider = timezoneProvider;
    }

    private readonly AnalysisApi analysisApi;
    private readonly TimezoneProviderService timezoneProvider;
    private readonly AuditApi audit;
}
