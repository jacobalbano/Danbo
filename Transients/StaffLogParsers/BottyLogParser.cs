using Danbo.Models;
using Discord;
using NodaTime.Extensions;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Danbo.Transients.StaffLogParsers;

public class BottyLogParser : IStaffLogParser
{
    public ulong Id => 583761567385124874;

    public IEnumerable<Infraction> ParseInfractions(IMessage message)
    {
        if (message.Embeds.Count != 1)
            yield break;

        var embed = message.Embeds.First();
        InfractionType? type = embed.Fields.FirstOrDefault(x => x.Name == "Action").Value switch
        {
            "ban" => InfractionType.Ban,
            "warn" => InfractionType.Warn,
            "mute" => InfractionType.Timeout,
            _ => null
        };

        if (type == null)
            yield break;

        if (!MentionUtils.TryParseUser(embed.Fields.FirstOrDefault(x => x.Name == "User").Value, out var userId)) yield break;
        if (!MentionUtils.TryParseUser(embed.Fields.FirstOrDefault(x => x.Name == "Responsible Mod").Value, out var modId)) yield break;
        var reason = embed.Fields.FirstOrDefault(x => x.Name == "Reason").Value;
        if (string.IsNullOrEmpty(reason)) yield break;
        var jumpLink = embed.Fields.FirstOrDefault(x => x.Name == "Message Link").Value;
        if (!string.IsNullOrEmpty(jumpLink))
            reason = $"{reason} ({jumpLink})";

        Duration? duration = null;
        if (type == InfractionType.Timeout)
        {
            var length = embed.Fields.FirstOrDefault(x => x.Name == "Length").Value;
            if (string.IsNullOrEmpty(length))
                yield break;

            var durationMatch = durationParser.Match(length);
            if (!durationMatch.Success)
                yield break;

            _ = int.TryParse(durationMatch.Groups[1].Value, out var d);
            _ = int.TryParse(durationMatch.Groups[2].Value, out var h);
            _ = int.TryParse(durationMatch.Groups[3].Value, out var m);
            duration = Duration.FromDays(d) + Duration.FromHours(h) + Duration.FromMinutes(m);
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

    private static readonly Regex durationParser = new(@"^(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?", RegexOptions.Compiled);
}
