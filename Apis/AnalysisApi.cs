using Danbo.Errors;
using Danbo.Models;
using Danbo.Models.Jobs;
using Danbo.Services;
using Danbo.TypeConverters;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using Discord.WebSocket;
using IdGen;
using LiteDB;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class AnalysisApi
{
    public async Task<GuildReport> Run(Guid jobKey, IProgress<GuildReport> progress = null)
    {
        UpdateMarker(AnalysisState.Pending);

        progress ??= new Progress<GuildReport>();
        var cts = new CancellationTokenSource();
        using var a = new Analyzer(guild, jobKey);

        var jobTask = Task.Run(async () =>
        {
            var result = await a.AnalyzeGuild(cts.Token);
            cts.Cancel();
            return result;
        });

        while (!cts.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            progress.Report(a.MakeReport(AnalysisState.Pending));

            if (cancelJob.TryRemove(jobKey, out _))
                cts.Cancel();
        }

        var result = await jobTask;
        progress.Report(result);

        UpdateMarker(result.State);
        return result;

        void UpdateMarker(AnalysisState state)
        {
            using var s = db.BeginSession();
            s.InsertOrUpdate((db.Select<AnalysisJob>()
                .Where(x => x.Key == jobKey)
                .FirstOrDefault() ?? new AnalysisJob()
                {
                    Key = jobKey,
                    FirstStarted = SystemClock.Instance.GetCurrentInstant()
                })
                with
                {
                    State = state
                });
        }
    }

    public void Cancel(Guid jobKey)
    {
        cancelJob[jobKey] = 0;
    }

    public IEnumerable<AnalysisJob> GetJobs()
    {
        return db.Select<AnalysisJob>()
            .ToEnumerable();
    }

    public AnalysisApi(ScopedGuildId gid, DiscordSocketClient client, GuildDb db)
    {
        if (gid.Id.HasValue)
            guild = client.GetGuild(gid.Id.Value);
        this.db = db;
    }

    private class Analyzer : IDisposable
    {
        public Analyzer(IGuild guild, Guid analysisKey)
        {
            this.guild = guild;
            store = new(new(guild.Id), analysisKey);
            userReports = store.Select<UserReport>()
                .ToEnumerable()
                .ToDictionary(x => x.UserId);

            channelReports = store.Select<ChannelReport>()
                .ToEnumerable()
                .ToDictionary(x => x.ChannelId);
        }

        public GuildReport MakeReport(AnalysisState resultState) => new()
        {
            Channels = channelReports.Values.ToList(),
            Users = userReports.Values.ToList(),
            State = resultState,
        };

        public async Task<GuildReport> AnalyzeGuild(CancellationToken ct)
        {
            await Initialize();

            foreach (var c in channelReports.Values)
            {
                switch (c.State)
                {
                    case AnalysisState.Pending:
                    case AnalysisState.Error:
                    case AnalysisState.Paused:
                        c.State = AnalysisState.Pending;
                        break;
                    case AnalysisState.Done:
                        continue;
                    default:
                        throw UnhandledEnumException.From(c.State);
                }

                try
                {
                    var channel = await guild.GetTextChannelAsync(c.ChannelId);
                    await foreach (var result in AnalyzeChannel(channel, c.ResumeMessageId, ct))
                    {
                        c.ProcessedMessages = result.ProcessedMessages;
                        c.ResumeMessageId = result.ResumeMessageId;
                        c.State = result.State;
                    }
                }
                catch (Exception)
                {
                    c.State = AnalysisState.Error;
                }

                if (ct.IsCancellationRequested)
                    return MakeReport(AnalysisState.Paused);
            }

            return MakeReport(AnalysisState.Done);
        }

        private async Task Initialize()
        {
            await guild.DownloadUsersAsync();
            foreach (var user in await guild.GetUsersAsync())
            {
                userReports.Establish(user.Id, id => new()
                {
                    UserId = id,
                    GuildJoinInstant = user.JoinedAt.HasValue ? user.JoinedAt.Value.ToInstant() : null
                });
            }

            foreach (var channel in await guild.GetTextChannelsAsync())
                channelReports.Establish(channel.Id, id => new() { ChannelId = id });
        }

        private static async IAsyncEnumerable<IReadOnlyCollection<IMessage>> ReadAllMessages(ITextChannel channel, ulong? resumeMessageId)
        {

            var reader = resumeMessageId.HasValue
                ? channel.GetMessagesAsync(resumeMessageId.Value, Direction.Before)
                : channel.GetMessagesAsync();

            while (true)
            {
                var messages = (await reader.FlattenAsync()).ToList();
                if (messages.Count == 0)
                    yield break;

                reader = channel.GetMessagesAsync(messages.Last().Id, Direction.Before);
                yield return messages;
            }
        }

        private async IAsyncEnumerable<ChannelReport> AnalyzeChannel(ITextChannel channel, ulong? resumeMessageId, [EnumeratorCancellation] CancellationToken ct)
        {
            var state = new ChannelReport { };

            await foreach (var chunk in ReadAllMessages(channel, resumeMessageId))
            {
                if (ct.IsCancellationRequested)
                    yield return state with { State = AnalysisState.Paused };

                foreach (var message in chunk)
                {
                    await AnalyzeReactions(message);

                    var messageTs = message.Timestamp.ToInstant();
                    var author = userReports.Establish(message.Author.Id, EstablishUser);
                    author.FirstMessageInstant = Instant.Min(author.FirstMessageInstant, messageTs);
                    author.LatestMessageInstant = Instant.Max(author.LatestMessageInstant, messageTs);
                    author.MessageCount++;
                    if (message.EditedTimestamp.HasValue)
                        author.MessagesEdited++;

                    state.ResumeMessageId = message.Id;
                }

                state.ProcessedMessages += (uint)chunk.Count;
                yield return state;
                await Task.Delay(rateLimitDelay, CancellationToken.None);
            }

            yield return state with { State = AnalysisState.Done };
        }

        private async Task AnalyzeReactions(IMessage message)
        {
            foreach (var reaction in message.Reactions)
            {
                foreach (var user in await message.GetReactionUsersAsync(reaction.Key, 100).FlattenAsync())
                    userReports.Establish(user.Id, EstablishUser)
                        .ReactionCount++;
            }
        }

        private static UserReport EstablishUser(ulong id) => new() { UserId = id };

        void IDisposable.Dispose()
        {
            using var s = store.BeginSession();
            s.InsertOrUpdate(userReports.Values);
            s.InsertOrUpdate(channelReports.Values);
        }

        public class Store : Database
        {
            public Store(ScopedGuildId gid, Guid jobId) : base(gid, jobId.ToString()) { }
        }

        private readonly Store store;
        private readonly Dictionary<ulong, UserReport> userReports;
        private readonly Dictionary<ulong, ChannelReport> channelReports;
        private readonly IGuild guild;

        // rate limit is 50 requests per second, so let's go 25 just to be safe
        private static readonly TimeSpan rateLimitDelay = TimeSpan.FromSeconds(1) / 25;
    }

    private static readonly ConcurrentDictionary<Guid, byte> cancelJob = new();
    private readonly SocketGuild guild;
    private readonly GuildDb db;
}
