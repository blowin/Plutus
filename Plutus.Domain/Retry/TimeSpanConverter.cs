using System.Text.Json;
using System.Text.Json.Serialization;
using Plutus.Domain.Retry;

namespace Plutus.Domain.Retry;

/// <summary>
/// JSON converter for <see cref="TimeSpan"/> that parses user-friendly formats
/// (e.g., "30s", "500ms", "1m") alongside ISO 8601 and standard TimeSpan formats.
/// </summary>
public sealed class TimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected JSON string, got {reader.TokenType}.");
        }

        var input = reader.GetString();
        if (input == null)
        {
            throw new JsonException("Cannot deserialize null as TimeSpan.");
        }

        return TimeSpanParser.Parse(input);
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(TimeSpanParser.ToString(value, TimeSpanFormat.Friendly));
    }
}
