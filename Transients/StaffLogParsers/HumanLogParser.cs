using Danbo.Models;
using Discord;
using NodaTime.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Joins;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Danbo.Transients.StaffLogParsers;

public class HumanLogParser : IStaffLogParser
{
    public ulong Id => 0; // catchall for humans

    public IEnumerable<Infraction> ParseInfractions(IMessage message)
    {
        string? action = null;
        var sb = new StringBuilder();
        foreach (var line in message.Content.ToLines())
        {
            if (guessUserId.IsMatch(line)) continue;
            sb.AppendLine(line);
            if (line.StartsWith("action", StringComparison.InvariantCultureIgnoreCase) && action == null)
                action = line;
        }

        var logMessage = sb.ToString();
        if (logMessage.Length == 0) yield break;

        action ??= logMessage;
        var baseInfraction = new Infraction()
        {
            Message = $"[Auto-imported! may contain issues]] {logMessage} - {action}",
            ModeratorId = message.Author.Id,
            Type = GuessActionType(action),
            InfractionInstant = message.Timestamp.ToInstant()
        };

        var mentionedUsers = guessUserId.Matches(message.Content)
            .Select(x => x.Groups[1].Value)
            .Select(x => (success: ulong.TryParse(x, out var userId), userId))
            .Where(x => x.success)
            .Select(x => x.userId);

        foreach (var userId in mentionedUsers)
            yield return baseInfraction with { Key = Guid.NewGuid(), UserId = userId };
    }

    private static InfractionType GuessActionType(string message)
    {
        return guessPatterns
            .SelectMany(x => x.pattern.Matches(message).Select(match => (x.type, match)))
            .OrderBy(x => x.match.Index)
            .Select(x => x.type)
            .FirstOrDefault(InfractionType.Other);
    }

    private static readonly Regex guessUserId = new(@"\<@\!?(\d{17,})\>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex guessBan = new(@"(insta|immediate)?\s?ban(ned)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex guessWarn = new(@"warn(ed|ings?)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex guessMute = new(@"(mute(d)?)|(time(d)?(.+?out))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex guessKick = new(@"kick(ed|ing)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly IReadOnlyList<(InfractionType type, Regex pattern)> guessPatterns = new[] {
        (InfractionType.Ban, guessBan),
        (InfractionType.Warn, guessWarn),
        (InfractionType.Timeout, guessMute),
        (InfractionType.Other, guessKick)
    };
}
