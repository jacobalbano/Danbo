using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Danbo.Apis;
using Danbo.Services;
using Microsoft.Extensions.Logging;
using Danbo.Models.Jobs;
using Danbo.Modules.Autocompletion;
using static Danbo.Apis.AnalysisApi;
using System.Text.Json;
using Danbo.Errors;
using System.Reactive;

namespace Danbo.Modules;

[Group("activity-analysis", "Server activity analysis")]
[RequireOwner, DefaultMemberPermissions(GuildPermission.ManageGuild)]
public class ActivityAnalysisModule : ModuleBase
{
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

        var lastProgress = new GuildReport();
        var message = await ack.ReplyAsync(MakeReport(lastProgress));
        var cts = new CancellationTokenSource();

        var jobTask = Task.Run(async () =>
        {
            var result = await analysisApi.Run(key, new Progress<GuildReport>(x => lastProgress = x));
            cts.Cancel();
            return result;
        });

        while (!cts.Token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            await message.ModifyAsync(x => x.Content = MakeReport(lastProgress));
        }

        var result = await jobTask;
        await message.ModifyAsync(x => x.Content = MakeReport(result));

        switch (result.State)
        {
            case AnalysisState.Done:
                var bytes = Encoding.UTF8.GetBytes(MakeDetailedReport(result));
                using (var stream = new MemoryStream(bytes))
                    await Context.Channel.SendFileAsync(stream, "activityAnalysis.txt", messageReference: message.Reference);
                break;
            case AnalysisState.Paused:
                await message.ReplyAsync("Analysis paused");
                return;
            default:
                throw UnhandledEnumException.From(result.State);
        }
    }

    [SlashCommand("stop", "Stop an ongoing analysis job")]
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

    static string MakeReport(GuildReport e)
    {
        var sb = new StringBuilder();

        var doneChannels = e.Channels
            .Where(x => x.State != AnalysisState.Pending)
            .ToList();

        int pending = 0, half = 3;
        var slice = e.Channels.TakeWhile(x => {
            if (x.State == AnalysisState.Pending && ++pending > half)
                return false;
            return true;
        }).TakeLast(half * 2)
            .ToList();

        var remaining = e.Channels.Count - slice.Count;
        sb.AppendLine($"**Messages processed:** {e.Channels.Select(x => (decimal)x.ProcessedMessages).Sum()}");
        sb.AppendLine($"**Channels processed** ({doneChannels.Count}/{e.Channels.Count}):");
        foreach (var c in slice)
            sb.AppendLine($"- {E(c.State)} {MentionUtils.MentionChannel(c.ChannelId)}");
        if (remaining > 0)
            sb.AppendLine($"...and {remaining} more");

        return sb.ToString();

        static string E(AnalysisState state)
        {
            return state switch
            {
                AnalysisState.Done => "✅",
                AnalysisState.Error => "⚠️",
                AnalysisState.Pending => "⌛",
                AnalysisState.Paused => "⏸️",
                _ => throw UnhandledEnumException.From(state)
            };
        }
    }

    private string MakeDetailedReport(GuildReport result)
    {
        var data = result.Users
            .Where(x => x.GuildJoinInstant != null)
            .OrderBy(x => x.MessageCount)
            .ThenBy(x => x.MessagesEdited)
            .ThenBy(x => x.ReactionCount)
            .ThenBy(x => x.GuildJoinInstant);

        var sb = new StringBuilder();
        var fields = new object[6] { "Id", "Mention", "JoinDate", "MessageCount", "MessagesEdited", "ReactionCount" };
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
            fields[5] = user.ReactionCount;
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
