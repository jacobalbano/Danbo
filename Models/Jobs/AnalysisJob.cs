using Danbo.TypeConverters;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models.Jobs;

/// <summary>
/// simply used as a marker for running jobs
/// all data for analysis is stored in a different database file
/// </summary>
public record class AnalysisJob : ModelBase
{
    public AnalysisState State { get; init; } = AnalysisState.Pending;
    
    [BsonConverter(typeof(NodaInstantBsonConverter))]
    public Instant FirstStarted { get; init; }
}

public enum AnalysisState { Pending, Running, Paused, Done,  Error }

public class GuildReport
{
    public AnalysisState State { get; set; }
    public IReadOnlyList<ChannelReport> Channels { get; init; } = Array.Empty<ChannelReport>();
    public IReadOnlyList<UserReport> Users { get; init; } = Array.Empty<UserReport>();
}

public record class UserReport : ModelBase
{
    public ulong UserId { get; init; }
    public uint MessageCount { get; set; }
    public uint MessagesEdited { get; set; }

    [BsonConverter(typeof(NodaInstantBsonConverter))]
    public Instant FirstMessageInstant { get; set; }

    [BsonConverter(typeof(NodaInstantBsonConverter))]
    public Instant LatestMessageInstant { get; set; }

    [BsonConverter(typeof(NodaInstantBsonConverter))]
    public Instant? GuildJoinInstant { get; set; }
}

public record class ChannelReport : ModelBase
{
    public ulong ChannelId { get; init; }
    public uint ProcessedMessages { get; set; }
    public ulong? ResumeMessageId { get; set; }
    public AnalysisState State { get; set; } = AnalysisState.Pending;
}
