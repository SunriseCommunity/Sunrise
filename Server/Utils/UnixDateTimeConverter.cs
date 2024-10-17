using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunrise.Server.Utils;

public class DateTimeUnixConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var unixTime = reader.GetInt64();
            return DateTimeOffset.FromUnixTimeMilliseconds(unixTime).DateTime;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return DateTime.Parse(reader.GetString());
        }

        throw new JsonException("Invalid DateTime or Unix time format");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var unixTime = ((DateTimeOffset)value).ToUnixTimeSeconds();
        writer.WriteNumberValue(unixTime);
    }
}