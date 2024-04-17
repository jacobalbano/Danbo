using Danbo.Models;
using Danbo.Models.Config;
using Danbo.Transients.StaffLogParsers;
using Danbo.Utility;
using Danbo.Utility.DependencyInjection;
using Discord;
using Discord.Net.Udp;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using NodaTime;
using NodaTime.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Danbo.Apis.RapsheetApi;
using static System.Net.WebRequestMethods;

namespace Danbo.Apis;

[AutoDiscoverScoped]
public class StaffApi
{
    public void SetStaffChannelProperty(string configProperty, ulong? channelId)
    {
        var config = db.Select<StaffChannelConfig>().FirstOrDefault() ?? new();
        config = configProperty switch
        {
            nameof(StaffChannelConfig.StaffLogChannelId) => config with { StaffLogChannelId = channelId },
            nameof(StaffChannelConfig.ServerLogChannelId) => config with { ServerLogChannelId = channelId },
            _ => throw new Exception($"Unhandled property {configProperty}"),
        };

        using var s = db.BeginSession();
        s.InsertOrUpdate(config);
    }

    public async Task PostToStaffLog(Infraction infraction)
    {
        var channelId = GetStaffLogChannelId() ?? throw new NullReferenceException(nameof(StaffChannelConfig.StaffLogChannelId));
        if (await discord.GetChannelAsync(channelId) is not ITextChannel staffLogChannel)
            throw new Exception("Error accessing staff log channel");

        var builder = new EmbedBuilder()
            .WithTitle(infraction.Type.ToString())
            .WithDescription(infraction.Message ?? "No message")
            .WithColor(infraction.Type.ToColor())
            .AddField("Moderator", MentionUtils.MentionUser(infraction.ModeratorId))
            .AddField("User", MentionUtils.MentionUser(infraction.UserId));
        if (infraction.Type == InfractionType.Timeout && infraction.Duration is Duration duration)
            builder.AddField("Duration", duration.ToString());

        await staffLogChannel.SendMessageAsync(embed: builder.Build());
    }

    public async Task<StaffLogExport> ExportStaffLog()
    {
        var channelId = GetStaffLogChannelId() ?? throw new NullReferenceException(nameof(StaffChannelConfig.StaffLogChannelId));
        if (await discord.GetChannelAsync(channelId) is not ITextChannel staffLogChannel)
            throw new Exception("Error accessing staff log channel; export failed");

        var infractions = new List<Infraction>();
        var unparsedUrls = new List<string>();

        if (!logParsers.TryGetValue(0, out var modParser))
            throw new Exception("No message parser found for moderator messages");

        foreach (var message in await staffLogChannel.GetMessagesAsync(int.MaxValue).FlattenAsync())
        {
            var length = infractions.Count;

            var results = Enumerable.Empty<Infraction>();
            if (logParsers.TryGetValue(message.Author.Id, out var botParser))
                results = botParser.ParseInfractions(message);
            else if (!message.Author.IsBot)
                results = modParser.ParseInfractions(message);

            infractions.AddRange(results);
            if (length == infractions.Count)
                unparsedUrls.Add(message.GetJumpUrl());
        }

        return new(infractions, unparsedUrls);
    }

    public async Task<StaffLogImportReport> ImportStaffLog(IAttachment attachment)
    {
        var content = await http.GetStringAsync(attachment.Url);
        var export = JsonSerializer.Deserialize<StaffLogExport>(content)!;
        int imported = 0, skipped = 0;

        using var s = db.BeginSession();
        foreach (var (userId, infractions) in export.Infractions.GroupBy(x => x.UserId))
        {
            var existingInfractions = s.Select<Infraction>()
                .Where(x => x.UserId == userId)
                .ToEnumerable() // TODO: see if we can get rid of this? here to prevent crash with IConvertable error
                .Select(x => x.InfractionInstant)
                .ToHashSet();

            foreach (var item in infractions)
            {
                if (existingInfractions.Contains(item.InfractionInstant))
                {
                    skipped++;
                    continue;
                }

                s.Insert(item);
                imported++;
            }
        }

        return new(imported, skipped, export.UnparsedMessageUrls.Count);
    }

    private ulong? GetStaffLogChannelId()
    {
        return db
            .Select<StaffChannelConfig>()
            .FirstOrDefault()
            ?.StaffLogChannelId;
    }

    public StaffApi(Database db, DiscordSocketClient discord, HttpClient http, ILogger<StaffApi> logger, IEnumerable<IStaffLogParser> logParsers)
    {
        this.db = db;
        this.discord = discord;
        this.http = http;
        this.logger = logger;
        this.logParsers = logParsers.ToDictionary(x => x.Id);
    }

    private readonly Database db;
    private readonly DiscordSocketClient discord;
    private readonly HttpClient http;
    private readonly ILogger<StaffApi> logger;
    private readonly IReadOnlyDictionary<ulong, IStaffLogParser> logParsers;

    public record class StaffLogExport(IReadOnlyList<Infraction> Infractions, IReadOnlyList<string> UnparsedMessageUrls);
    public record class StaffLogImportReport(int Imported, int Skipped, int Unparsed)
    {
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Imported} infractions imported");
            sb.AppendLine($"{Skipped} existing infractions skipped");
            sb.Append($"{Unparsed} messages failed to parse");
            return sb.ToString();
        }
    }
}
