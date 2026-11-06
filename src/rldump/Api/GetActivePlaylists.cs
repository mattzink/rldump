using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Api;

/// <summary>
/// Playlists/GetActivePlaylists takes no body (an empty object) and returns
/// the playlists currently live on the servers, split into casual and ranked.
/// </summary>
internal sealed class GetActivePlaylistsRequest
{
}

/// <summary>
/// Wire model for one active-playlist entry. Only the fields that need more
/// than a pass-through are declared explicitly: the timestamps (epoch seconds
/// or null → ISO 8601), the b-prefixed flag (bX → x in the tidy output), and
/// the playlist index (named via Config/GetPlaylists in the tidy output).
/// Every other field (NodeID, Type, DisparitySettings, …) flows through
/// <see cref="ExtensionData"/>.
/// </summary>
internal sealed class ActivePlaylistEntry
{
    /// <summary>Playlist index; named via Config/GetPlaylists in the tidy output.</summary>
    public int Playlist { get; init; }

    /// <summary>Unix epoch seconds; null when the entry has no scheduled start.</summary>
    public long? StartTime { get; init; }

    /// <summary>Unix epoch seconds; null when the entry has no scheduled end.</summary>
    public long? EndTime { get; init; }

    /// <summary>
    /// Present on some entries per the game's reflected contract; absent
    /// otherwise. Stripped to "competitive" in the tidy output.
    /// </summary>
    public bool? bCompetitive { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// The <c>Result</c> payload of Playlists/GetActivePlaylists. Only the two
/// playlist arrays are declared explicitly; any other field the API sends
/// (RankedUnlock, XPLevelUnlocked, or new fields) is captured in
/// <see cref="ExtensionData"/> and passed through.
/// </summary>
internal sealed class GetActivePlaylistsResult
{
    public List<ActivePlaylistEntry> CasualPlaylists { get; init; } = [];

    public List<ActivePlaylistEntry> RankedPlaylists { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
