using System.Globalization;
using Rldump.Api;

namespace Rldump.Output;

/// <summary>
/// Maps the GetActivePlaylists API result to the tidy output model: each
/// entry's raw playlist index keeps its value and gains a title alongside it
/// (from Config/GetPlaylists, "Unknown" when unmapped), present start/end
/// timestamps become ISO 8601 UTC (null ones stay omitted), bCompetitive is
/// stripped to competitive, and extension data passes through with camelCased
/// keys.
/// </summary>
internal static class ActivePlaylistsOutputMapper
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static ActivePlaylistsOutput Map(GetActivePlaylistsResult result, IReadOnlyDictionary<int, string> playlistNames)
    {
        return new ActivePlaylistsOutput
        {
            CasualPlaylists = result.CasualPlaylists.Select(entry => MapEntry(entry, playlistNames)).ToArray(),
            RankedPlaylists = result.RankedPlaylists.Select(entry => MapEntry(entry, playlistNames)).ToArray(),
            ExtensionData = TidyJson.CamelCaseKeys(result.ExtensionData),
        };
    }

    private static ActivePlaylistOut MapEntry(ActivePlaylistEntry entry, IReadOnlyDictionary<int, string> playlistNames) =>
        new()
        {
            Playlist = entry.Playlist,
            PlaylistName = playlistNames.TryGetValue(entry.Playlist, out string? name) ? name! : "Unknown",
            StartTime = ToIso8601Utc(entry.StartTime),
            EndTime = ToIso8601Utc(entry.EndTime),
            Competitive = entry.bCompetitive,
            ExtensionData = TidyJson.CamelCaseKeys(entry.ExtensionData),
        };

    /// <summary>Unix seconds → "yyyy-MM-ddTHH:mm:ssZ" (UTC); null stays null.</summary>
    private static string? ToIso8601Utc(long? unixSeconds) =>
        unixSeconds is long s
            ? DateTimeOffset.FromUnixTimeSeconds(s).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Invariant)
            : null;
}
