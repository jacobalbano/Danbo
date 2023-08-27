using Danbo.Models;
using Discord;
using NodaTime;
using NodaTime.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Danbo.Transients.StaffLogParsers;

public class DynoLogParser : IStaffLogParser
{
    public ulong Id => 155149108183695360;

    public IEnumerable<Infraction> ParseInfractions(IMessage message)
    {
        if (message.Embeds.Count != 1)
            yield break;

        var embed = message.Embeds.First();
        var typeMatch = titleParser.Match(embed.Title);
        if (!typeMatch.Success)
            yield break;

        InfractionType? type = typeMatch.Captures[1].Value switch
        {
            "Ban" => InfractionType.Ban,
            "Kick" => InfractionType.Other,
            "Warn" => InfractionType.Warn,
            "Mute" => InfractionType.Timeout,
            _ => null
        };

        if (type == null)
            yield break;

        if (!MentionUtils.TryParseUser(embed.Fields.FirstOrDefault(x => x.Name == "User").Value, out var userId)) yield break;
        if (!MentionUtils.TryParseUser(embed.Fields.FirstOrDefault(x => x.Name == "Moderator").Value, out var modId)) yield break;
        var reason = embed.Fields.FirstOrDefault(x => x.Name == "Reason").Value;
        if (string.IsNullOrEmpty(reason)) yield break;

        Duration? duration = null;
        if (type == InfractionType.Timeout)
        {
            var length = embed.Fields.FirstOrDefault(x => x.Name == "Length").Value;
            if (string.IsNullOrEmpty(length))
                yield break;

            var durationMatch = durationParser.Match(length);
            if (!durationMatch.Success)
                yield break;

            _ = int.TryParse(durationMatch.Groups[1].Value, out var m);
            duration = Duration.FromMinutes(m);
        }

        yield return new()
        {
            InfractionInstant = message.Timestamp.ToInstant(),
            Message = reason,
            Duration = duration,
            Type = type.Value,
            ModeratorId = modId,
            UserId = userId
        };
    }

    private static readonly Regex titleParser = new(@"Case \d+? \| (\w+?) ", RegexOptions.Compiled);
    private static readonly Regex durationParser = new(@"^(\d+) minutes", RegexOptions.Compiled);
}
