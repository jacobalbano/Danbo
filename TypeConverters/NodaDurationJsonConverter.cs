using NodaTime;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Danbo.TypeConverters;

public class NodaDurationJsonConverter : JsonConverter<Duration>
{
    public override Duration Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Duration.FromTicks(reader.GetDouble());
    }

    public override void Write(Utf8JsonWriter writer, Duration value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.TotalTicks);
    }
}