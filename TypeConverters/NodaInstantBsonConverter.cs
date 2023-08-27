using Danbo.Models;
using LiteDB;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.TypeConverters;

internal class NodaInstantBsonConverter : IBsonConverter<Instant>
{
    public Instant Deserialize(BsonValue value)
    {
        return Instant.FromUnixTimeTicks(value.AsInt64);
    }

    public BsonValue Serialize(Instant value)
    {
        return value.ToUnixTimeTicks();
    }
}