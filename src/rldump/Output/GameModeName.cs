using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Output;

/// <summary>
/// A game-mode value for the tidy output: the mode name when the index is
/// known in the embedded mapping, otherwise the raw index number. Serializes
/// as a JSON string (known) or number (unknown) — hence the
/// <c>string | number</c> field type in the tidy output.
/// </summary>
[JsonConverter(typeof(GameModeNameConverter))]
internal readonly record struct GameModeName(int Index, string? Name);

/// <summary>
/// Writes <see cref="GameModeName"/> as a string when the mode is known and as
/// the raw number otherwise. This is the only custom converter in the tool.
/// </summary>
internal sealed class GameModeNameConverter : JsonConverter<GameModeName>
{
    public override GameModeName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return new GameModeName(reader.GetInt32(), null);
        throw new JsonException($"game mode values must be numbers, got token {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, GameModeName value, JsonSerializerOptions options)
    {
        if (value.Name is not null)
            writer.WriteStringValue(value.Name);
        else
            writer.WriteNumberValue(value.Index);
    }
}
