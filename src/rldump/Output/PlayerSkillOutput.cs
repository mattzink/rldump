using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Output;

/// <summary>Top-level tidy output for GetPlayerSkill: { "skills": [ … ] } plus pass-through fields (e.g. "rewardLevels").</summary>
internal sealed class PlayerSkillOutput
{
    public SkillOut[] Skills { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Tidy output for one skill entry. The raw playlist index is kept and the
/// playlist title (via Config/GetPlaylists) is added alongside it as
/// <see cref="PlaylistName"/>; every other wire field is carried in
/// <see cref="ExtensionData"/> with its key camelCased by the mapper (the
/// serializer writes extension-data keys verbatim, so the naming policy does
/// not apply to them).
/// </summary>
internal sealed class SkillOut
{
    /// <summary>The raw playlist index; omitted when absent on the wire.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Playlist { get; init; }

    /// <summary>The playlist title when known, otherwise "Unknown".</summary>
    public string PlaylistName { get; init; } = "";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
