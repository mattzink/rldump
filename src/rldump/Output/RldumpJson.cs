using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Rldump.Output;

/// <summary>
/// Source-generated context for the tidy output (camelCase keys). Source
/// generation is mandatory: the binary ships as Native AOT and cannot use
/// reflection-based serialization.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ScheduleOutput))]
[JsonSerializable(typeof(PlayerSkillOutput))]
[JsonSerializable(typeof(ActivePlaylistsOutput))]
internal sealed partial class OutputJsonContext : JsonSerializerContext
{
}

/// <summary>Serializes a tidy output object to a compact JSON string.</summary>
internal static class JsonSerialize
{
    public static string Schedule(ScheduleOutput output) =>
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(output, OutputJsonContext.Default.ScheduleOutput));

    public static string Skills(PlayerSkillOutput output) =>
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(output, OutputJsonContext.Default.PlayerSkillOutput));

    public static string ActivePlaylists(ActivePlaylistsOutput output) =>
        Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(output, OutputJsonContext.Default.ActivePlaylistsOutput));
}

/// <summary>
/// Renders compact JSON as indented output unless compact is requested.
/// Re-parsing through <see cref="JsonNode"/> is AOT-safe (no reflection) and
/// preserves property order and values exactly.
/// </summary>
internal static class JsonFormat
{
    public static string Format(string compactJson, bool compact) =>
        compact
            ? compactJson
            : JsonNode.Parse(compactJson)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
