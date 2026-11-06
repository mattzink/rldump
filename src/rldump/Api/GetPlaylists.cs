namespace Rldump.Api;

/// <summary>
/// Config/GetPlaylists takes no body (an empty object) and returns every
/// playlist the game knows about. Hidden playlists carry a null Title; only
/// the fields the playlist-name mapping needs are modeled.
/// </summary>
internal sealed class GetPlaylistsRequest
{
}

internal sealed class PlaylistEntry
{
    public int ID { get; init; }

    /// <summary>Null for hidden playlists (they have no name to map to).</summary>
    public string? Title { get; init; }
}

internal sealed class GetPlaylistsResult
{
    public List<PlaylistEntry> Playlists { get; init; } = [];
}
