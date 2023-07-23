using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Danbo.TypeConverters;

namespace Danbo.Models.Jobs;

public record class OntopicExpirationJob : ModelBase
{
    [BsonConverter(typeof(NodaInstantBsonConverter))]
    public Instant Expiration { get; init; }

    public ulong UserId { get; init; }

    public ulong JobHandle { get; init; }
}
