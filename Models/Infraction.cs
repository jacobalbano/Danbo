using Danbo.TypeConverters;
using Discord;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Danbo.Models;

public enum InfractionType
{
    Note,
    Warn,
    Timeout,
    Ban,
    Other,
}

public record class Infraction : ModelBase
{
    [Indexed, JsonConverter(typeof(JsonStringEnumConverter))]
    public InfractionType Type { get; init; }

    public ulong ModeratorId { get; init; }

    [BsonConverter(typeof(NodaInstantBsonConverter)), JsonConverter(typeof(NodaInstantJsonConverter))]
    public Instant InfractionInstant  { get; init; }

    [BsonConverter(typeof(NodaDurationBsonConverter)), JsonConverter(typeof(NodaDurationJsonConverter))]
    public Duration? Duration { get; init; }

    public string Message { get; init; }

    [Indexed]
    public ulong UserId { get; init; }
}

public static class InfractionExtensions
{
    public static string ToEmoji(this InfractionType type) => type switch
    {
        InfractionType.Note => "📝",
        InfractionType.Warn => "⚠️",
        InfractionType.Timeout => "🛑",
        InfractionType.Ban => "🔨",
        _ => "❔"
    };

    public static Color ToColor(this InfractionType type) => type switch
    {
        InfractionType.Note => Color.Default,
        InfractionType.Warn => new Color(250, 219, 94),
        InfractionType.Timeout => Color.Orange,
        InfractionType.Ban => Color.Red,
        _ => Color.Default
    };
}