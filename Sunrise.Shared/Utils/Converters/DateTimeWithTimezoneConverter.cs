using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunrise.Shared.Utils.Converters;

public class DateTimeWithTimezoneConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Nullify the timezone offset and remove it from string

        var dto = DateTimeOffset.Parse(reader.GetString()!);
        var output = dto.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff");

        return DateTime.Parse(output);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // TODO: We currently have all time in UTC+0, so we can just append +00:00
        // In the future would be good to migrate database to DataTimeOffset

        writer.WriteStringValue(value.ToString("yyyy-MM-ddTHH:mm:ss.fffffff") + "+00:00");
    }
}