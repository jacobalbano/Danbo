﻿using Danbo.Errors;
using Danbo.Models;
using Danbo.Models.Jobs;
using Danbo.Services;
using Danbo.TypeConverters;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using Discord.Net;
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
using System.Threading;
using System.Threading.Tasks;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class AnalysisApi
{
    public async Task<GuildReport> Run(Guid jobKey, IProgress<IReadOnlyList<ChannelReport>> progress = null)
    {
        UpdateMarker(AnalysisState.Running);

        progress ??= new Progress<IReadOnlyList<ChannelReport>>();
        var blacklist = GetBlacklist()
            .Select(x => x.ChannelId)
            .ToHashSet();

        using var a = new Analyzer(guild, jobKey, logger, blacklist);

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                progress.Report(a.ChannelReports().ToList());

                if (cancelJob.TryRemove(jobKey, out _))
                    cts.Cancel();
            }
        });

        var result = await a.AnalyzeGuild(cts.Token);
        cts.Cancel();
        progress.Report(result.Channels);

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

    public void Cleanup()
    {
        // TODO: delete the backing databases
        using var s = db.BeginSession();
        s.Delete(GetJobs()
            .Where(x => x.State != AnalysisState.Running));
    }

    public IEnumerable<AnalysisJob> GetJobs()
    {
        return db.Select<AnalysisJob>()
            .ToEnumerable();
    }

    public IEnumerable<AnalysisBlacklist> GetBlacklist()
    {
        return db.Select<AnalysisBlacklist>()
            .ToEnumerable();
    }

    public void RemoveBlacklist(ITextChannel channel)
    {
        using var s = db.BeginSession();
        var entry = s.Select<AnalysisBlacklist>()
            .Where(x => x.ChannelId == channel.Id)
            .FirstOrDefault();

        if (entry != null)
            s.Delete(entry);
    }

    public void AddBlacklist(ITextChannel channel)
    {
        using var s = db.BeginSession();
        var entry = s.Select<AnalysisBlacklist>()
            .Where(x => x.ChannelId == channel.Id)
            .FirstOrDefault() ?? new() { ChannelId = channel.Id };
        s.InsertOrUpdate(entry);
    }

    public AnalysisApi(ScopedGuildId gid, DiscordSocketClient client, GuildDb db, ILogger<AnalysisApi> logger)
    {
        if (gid.Id.HasValue)
            guild = client.GetGuild(gid.Id.Value);
        this.db = db;
        this.logger = logger;
    }

    private class Analyzer : IDisposable
    {
        public Analyzer(IGuild guild, Guid analysisKey, ILogger logger, IReadOnlySet<ulong> channelBlacklist)
        {
            this.guild = guild;
            this.logger = logger;
            this.channelBlacklist = channelBlacklist;
            store = new(new(guild.Id), analysisKey);
            userReports = store.Select<UserReport>()
                .ToEnumerable()
                .ToDictionary(x => x.UserId);

            channelReports = store.Select<ChannelReport>()
                .ToEnumerable()
                .Where(x => !channelBlacklist.Contains(x.ChannelId))
                .ToDictionary(x => x.ChannelId);
        }

        public IEnumerable<ChannelReport> ChannelReports()
        {
            var keys = channelReports.Keys.ToList();
            return keys.Select(x => channelReports[x])
                .OrderByDescending(x => x.ChannelId);
        }

        public GuildReport MakeFullReport(AnalysisState resultState) => new()
        {
            Channels = channelReports.Values.ToList(),
            Users = userReports.Values.ToList(),
            State = resultState,
        };

        public async Task<GuildReport> AnalyzeGuild(CancellationToken ct)
        {
            await Initialize();
            var periodic = new InfrequentActor(Duration.FromMinutes(5));

            foreach (var c in ChannelReports())
            {
                switch (c.State)
                {
                    case AnalysisState.Pending:
                    case AnalysisState.Error:
                    case AnalysisState.Paused:
                        c.State = AnalysisState.Running;
                        break;
                    case AnalysisState.Done: continue;
                    default: throw UnhandledEnumException.From(c.State);
                }

                try
                {
                    var channel = await guild.GetTextChannelAsync(c.ChannelId);
                    if (channel == null)
                    {
                        c.State = AnalysisState.Error;
                        logger.LogWarning("Couldn't load channel {channelId} for analysis", c.ChannelId);
                        continue;
                    }

                    await foreach (var result in AnalyzeChannel(channel, c.ResumeMessageId, ct))
                    {
                        c.ProcessedMessages += result.ProcessedMessages;
                        c.ResumeMessageId = result.ResumeMessageId ?? c.ResumeMessageId;
                        c.State = result.State;
                        periodic.Act(() => Save());
                    }
                }
                catch (Exception e)
                {
                    c.State = AnalysisState.Error;
                    logger.LogError(e, "Error processing channel {channelId}", c.ChannelId);
                }

                if (ct.IsCancellationRequested)
                    return MakeFullReport(AnalysisState.Paused);
            }

            return MakeFullReport(AnalysisState.Done);
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

            var channels = new List<ITextChannel>();
            channels.AddRange(await guild.GetTextChannelsAsync());
            channels.AddRange(await guild.GetThreadChannelsAsync());
            foreach (var forum in await guild.GetForumChannelsAsync())
                channels.AddRange(await forum.GetActiveThreadsAsync());

            foreach (var channel in channels.Where(x => !channelBlacklist.Contains(x.Id)))
            {
                var report = channelReports.Establish(channel.Id, id => new() { ChannelId = id });
                // clean up after a crash
                if (report.State == AnalysisState.Running)
                    report.State = AnalysisState.Pending;
            }
        }

        private async IAsyncEnumerable<IReadOnlyCollection<IMessage>> ReadAllMessages(ITextChannel channel, ulong? resumeMessageId)
        {
            var reader = resumeMessageId.HasValue
                ? channel.GetMessagesAsync(resumeMessageId.Value, Direction.Before)
                : channel.GetMessagesAsync();

            while (true)
            {
                var messages = await Retry(reader);
                if (messages.Count == 0)
                    yield break;

                yield return messages;
                reader = channel.GetMessagesAsync(messages[^1].Id, Direction.Before);
                await Task.Delay(rateLimitDelay);
            }

            async Task<IReadOnlyList<IMessage>> Retry(IAsyncEnumerable<IReadOnlyCollection<IMessage>> reader)
            {
                int attempts = 0;
                while (true)
                {
                    try { return (await reader.FlattenAsync()).ToList(); }
                    catch (HttpException)
                    {
                        if (++attempts > 5) throw;
                        logger.LogWarning("Got HTTP error; waiting {seconds} seconds to retry", attempts);
                        await Task.Delay(TimeSpan.FromSeconds(attempts));
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, "Got unhandled error");
                        throw;
                    }
                }
            }
        }

        private async IAsyncEnumerable<ChannelReport> AnalyzeChannel(ITextChannel channel, ulong? resumeMessageId, [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var chunk in ReadAllMessages(channel, resumeMessageId))
            {
                var state = new ChannelReport { State = AnalysisState.Running };
                if (ct.IsCancellationRequested)
                {
                  yield return state with { State = AnalysisState.Paused };
                    yield break;
                }

                foreach (var message in chunk)
                {
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

            yield return new() { State = AnalysisState.Done };
        }

        private static UserReport EstablishUser(ulong id) => new() { UserId = id };

        void IDisposable.Dispose() => Save();

        public void Save()
        {
            using var s = store.BeginSession();
            s.InsertOrUpdate(userReports.Values);
            s.InsertOrUpdate(channelReports.Values);
            logger.LogDebug("Saved analysis state");
        }

        public class Store : Database
        {
            public Store(ScopedGuildId gid, Guid jobId) : base(gid, jobId.ToString()) { }
        }

        private readonly Store store;
        private readonly Dictionary<ulong, UserReport> userReports;
        private readonly Dictionary<ulong, ChannelReport> channelReports;
        private readonly IGuild guild;
        private readonly ILogger logger;
        private readonly IReadOnlySet<ulong> channelBlacklist;

        // rate limit is 50 requests per second, so let's go 25 just to be safe
        private static readonly TimeSpan rateLimitDelay = TimeSpan.FromSeconds(1) / 25;
    }

    private static readonly ConcurrentDictionary<Guid, byte> cancelJob = new();
    private readonly SocketGuild guild;
    private readonly GuildDb db;
    private readonly ILogger logger;
}
