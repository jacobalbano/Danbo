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

public class RaiLogParser : IStaffLogParser
{
    public ulong Id => 270366726737231884;

    public IEnumerable<Infraction> ParseInfractions(IMessage message)
    {
        if (message.Embeds.Count != 1)
            yield break;

        var embed = message.Embeds.First();

        InfractionType? type = (embed.Title ?? embed.Description) switch
        {
            "Ban" => InfractionType.Ban,
            "Warn" => InfractionType.Warn,
            "Temporary Mute" => InfractionType.Timeout,
            "Mute" => InfractionType.Timeout,
            "Timeout" => InfractionType.Timeout,
            _ => null
        };

        if (type == null)
            yield break;

        var userMatch = idParser.Match(embed.Fields.FirstOrDefault(x => x.Name == "User").Value);
        if (!userMatch.Success) yield break;
        if (!ulong.TryParse(userMatch.Groups[1].Value, out var userId)) yield break;

        if (embed.Footer == null) yield break;
        var modMatch = idParser.Match(embed.Footer.Value.Text);
        if (!modMatch.Success) yield break;
        if (!ulong.TryParse(modMatch.Groups[1].Value, out var modId)) yield break;

        var reason = embed.Fields.FirstOrDefault(x => x.Name == "Reason").Value;
        if (string.IsNullOrEmpty(reason)) yield break;

        var jumpLink = embed.Fields.FirstOrDefault(x => x.Name == "Jump URL").Value;
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
            Duration = duration,
            Message = reason,
            Type = type.Value,
            ModeratorId = modId,
            UserId = userId
        };
    }

    private static readonly Regex idParser = new(@"\((\d{17,})\)", RegexOptions.Compiled);
    private static readonly Regex durationParser = new(@"^(?:(\d+)d)?(?:(\d+)h)?(?:(\d+)m)?", RegexOptions.Compiled);
}
