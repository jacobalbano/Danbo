using Danbo.Models;
using LiteDB;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Danbo.TypeConverters;

internal class NodaDurationBsonConverter : IBsonConverter<Duration?>
{
    public Duration? Deserialize(BsonValue value)
    {
        return value.IsNull ? null : Duration.FromTicks(value.AsDouble);
    }

    public BsonValue Serialize(Duration? value)
    {
        return new BsonValue(value?.TotalTicks);
    }
}
