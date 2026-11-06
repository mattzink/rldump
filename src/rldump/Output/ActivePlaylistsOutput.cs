using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Output;

/// <summary>
/// Top-level tidy output for GetActivePlaylists:
/// { "casualPlaylists": [ … ], "rankedPlaylists": [ … ] } plus pass-through
/// fields (e.g. "rankedUnlock", "xpLevelUnlocked").
/// </summary>
internal sealed class ActivePlaylistsOutput
{
    public ActivePlaylistOut[] CasualPlaylists { get; init; } = [];

    public ActivePlaylistOut[] RankedPlaylists { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Tidy output for one active-playlist entry. The raw playlist index is kept
/// and the playlist title (via Config/GetPlaylists) is added alongside it as
/// <see cref="PlaylistName"/>; start/end times are ISO 8601 UTC (omitted when
/// null on the wire); bCompetitive is stripped to <see cref="Competitive"/>.
/// Every other wire field is carried in <see cref="ExtensionData"/> with its
/// key camelCased by the mapper (the serializer writes extension-data keys
/// verbatim, so the naming policy does not apply to them).
/// </summary>
internal sealed class ActivePlaylistOut
{
    /// <summary>The raw playlist index from the wire.</summary>
    public int Playlist { get; init; }

    /// <summary>The playlist title when known, otherwise "Unknown".</summary>
    public string PlaylistName { get; init; } = "";

    /// <summary>ISO 8601 UTC; omitted when absent or null on the wire.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartTime { get; init; }

    /// <summary>ISO 8601 UTC; omitted when absent or null on the wire.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndTime { get; init; }

    /// <summary>Omitted when absent or null on the wire.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Competitive { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
