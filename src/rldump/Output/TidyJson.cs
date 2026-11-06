using System.Text;
using System.Text.Json;

namespace Rldump.Output;

/// <summary>
/// Tidy-output helpers for key casing.
///
/// The tidy contract (non-<c>--raw</c> only) that the mappers in this
/// namespace enforce:
/// <list type="bullet">
/// <item>IDs that exceed JavaScript safe-integer precision are strings;
/// timestamps are ISO 8601 UTC (<c>yyyy-MM-ddTHH:mm:ssZ</c>);
/// <c>b</c>-prefixed wire fields lose the prefix.</item>
/// <item>Raw index fields keep their wire numbers and gain the name
/// alongside — <c>gameMode</c> → <c>gameModeName</c>, <c>gameModes</c> →
/// <c>gameModeNames</c> (a parallel array), <c>playlist</c> →
/// <c>playlistName</c> — never replacing the value. A name the mapping
/// doesn't know (absent from it, or a playlist with no title) is the string
/// <c>"Unknown"</c>.</item>
/// <item>Endpoints whose fields need no value transform (e.g. player find,
/// playlists all) print the <c>Result</c> payload with all object keys
/// recursively camelCased; values stay verbatim.</item>
/// </list>
///
/// Two roles:
/// <list type="bullet">
/// <item><see cref="CamelCaseKeys(JsonElement)"/> — the pass-through transform
/// for the untransformed endpoints above: recursively camelCase every object
/// key, values verbatim.</item>
/// <item><see cref="CamelCaseKeys(Dictionary{string, JsonElement}?)"/> — the
/// extension-data key casing shared by the modeled-output mappers (the
/// serializer writes extension-data keys verbatim, so they must be camelCased
/// before serialization).</item>
/// </list>
/// </summary>
internal static class TidyJson
{
    /// <summary>CamelCases all object keys; returns the result as compact JSON.</summary>
    public static string CamelCaseKeys(JsonElement element)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
            WriteCamel(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Copies extension data, camelCasing each key — and, recursively, the
    /// keys of any nested objects, since the serializer writes extension-data
    /// values verbatim (the naming policy does not apply to them), so the
    /// tidy camelCase contract must hold at every level. Primitives pass
    /// through untouched. CamelCase is idempotent on already-camelCased names,
    /// so keys that were added by the API in either casing come out
    /// consistently.
    /// </summary>
    public static Dictionary<string, JsonElement>? CamelCaseKeys(Dictionary<string, JsonElement>? data)
    {
        if (data is null)
            return null;

        Dictionary<string, JsonElement> result = new(data.Count);
        foreach ((string key, JsonElement value) in data)
            result[JsonNamingPolicy.CamelCase.ConvertName(key)] = CamelCaseValue(value);
        return result;
    }

    /// <summary>Recursively camelCases the keys of an element; primitives pass through as-is.</summary>
    private static JsonElement CamelCaseValue(JsonElement value) =>
        value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? JsonDocument.Parse(CamelCaseKeys(value)).RootElement.Clone()
            : value;

    private static void WriteCamel(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    writer.WritePropertyName(JsonNamingPolicy.CamelCase.ConvertName(property.Name));
                    WriteCamel(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                    WriteCamel(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
