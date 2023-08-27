using Danbo.TypeConverters;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.Models.Jobs;

public record class EventMuteExpirationJob : ModelBase
{
    public ulong UserId { get; init; }

    public ulong GuildEventId { get; init; }
}
