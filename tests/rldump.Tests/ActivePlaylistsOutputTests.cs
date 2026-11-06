using System.Text.Json;
using System.Text.Json.Nodes;
using Rldump.Api;
using Rldump.Output;

namespace Rldump.Tests;

/// <summary>
/// Fixture-driven tests of the active-playlists tidy transformation. The
/// success fixture is a verbatim copy of a real API response — do not edit
/// it; it is the ground truth for the wire shape.
/// </summary>
public class ActivePlaylistsOutputTests
{
    private static GetActivePlaylistsResult LoadFixture() =>
        JsonSerializer.Deserialize(Fixtures.Read("getactiveplaylists-success.json"), ApiJsonContext.Default.EnvelopeGetActivePlaylistsResult)!.Result!;

    /// <summary>
    /// The live playlist-name map, built from the (also verbatim) catalog
    /// fixture exactly as the app builds it: ID → title, entries without a
    /// title dropped.
    /// </summary>
    private static Dictionary<int, string> Names()
    {
        GetPlaylistsResult catalog = JsonSerializer.Deserialize(
            Fixtures.Read("getplaylists-success.json"), ApiJsonContext.Default.EnvelopeGetPlaylistsResult)!.Result!;
        Dictionary<int, string> names = new(catalog.Playlists.Count);
        foreach (PlaylistEntry playlist in catalog.Playlists)
            if (!string.IsNullOrEmpty(playlist.Title))
                names.TryAdd(playlist.ID, playlist.Title);
        return names;
    }

    private static GetActivePlaylistsResult Deserialize(JsonNode node) =>
        JsonSerializer.Deserialize(node.ToJsonString(), ApiJsonContext.Default.EnvelopeGetActivePlaylistsResult)!.Result!;

    [Fact]
    public void Map_Fixture_ProducesExpectedTidyJson()
    {
        ActivePlaylistsOutput output = ActivePlaylistsOutputMapper.Map(LoadFixture(), Names());
        string actual = JsonSerialize.ActivePlaylists(output);

        string expected = Fixtures.Read("getactiveplaylists-tidy-expected.json");
        Assert.Equal(JsonNode.Parse(expected)!.ToJsonString(), JsonNode.Parse(actual)!.ToJsonString());
    }

    [Fact]
    public void Map_NullTimestamps_Omitted_RawIndexStaysNumber()
    {
        ActivePlaylistsOutput output = ActivePlaylistsOutputMapper.Map(LoadFixture(), Names());

        // The fixture's first casual entry has StartTime/EndTime null on the
        // wire; the transformed keys are omitted, the raw index stays a
        // number, and the name sits alongside it.
        ActivePlaylistOut first = output.CasualPlaylists[0];
        Assert.Null(first.StartTime);
        Assert.Null(first.EndTime);
        Assert.Null(first.Competitive);
        Assert.Equal(1, first.Playlist);
        Assert.Equal("Duel", first.PlaylistName);

        string json = JsonSerialize.ActivePlaylists(output);
        JsonArray casual = (JsonArray)JsonNode.Parse(json)!["casualPlaylists"]!;
        JsonNode entry = casual[0]!;
        Assert.Null(entry["startTime"]);
        Assert.Null(entry["endTime"]);
        Assert.Null(entry["competitive"]);
        Assert.Equal(1, entry["playlist"]!.GetValue<int>());
    }

    [Fact]
    public void Map_PresentTimestamps_Iso8601Utc()
    {
        ActivePlaylistsOutput output = ActivePlaylistsOutputMapper.Map(LoadFixture(), Names());

        // Fixture entry LargePartyCasual: 1793811600 / 1794416400.
        ActivePlaylistOut largeParty = output.CasualPlaylists[4];
        Assert.Equal("2026-11-04T17:00:00Z", largeParty.StartTime);
        Assert.Equal("2026-11-11T17:00:00Z", largeParty.EndTime);

        // A ranked entry with a start but no end.
        ActivePlaylistOut firstRanked = output.RankedPlaylists[0];
        Assert.Equal("2026-09-23T16:00:00Z", firstRanked.StartTime);
        Assert.Null(firstRanked.EndTime);
    }

    [Fact]
    public void Map_UnknownPlaylist_RawIndexStaysNumber_WithNameUnknown()
    {
        JsonNode node = JsonNode.Parse(Fixtures.Read("getactiveplaylists-success.json"))!;
        ((JsonArray)node["Result"]!["CasualPlaylists"]!)[0]!["Playlist"] = 999;

        ActivePlaylistsOutput output = ActivePlaylistsOutputMapper.Map(Deserialize(node), Names());
        JsonArray casual = (JsonArray)JsonNode.Parse(JsonSerialize.ActivePlaylists(output))!["casualPlaylists"]!;
        JsonNode first = casual[0]!;

        // Unknown indices stay raw JSON numbers (GetValue<int> would throw if
        // a string had been written), and their name comes out "Unknown".
        Assert.Equal(999, first["playlist"]!.GetValue<int>());
        Assert.Equal("Unknown", (string?)first["playlistName"]!);
    }

    [Fact]
    public void Map_BCompetitive_StrippedToCompetitive()
    {
        JsonNode node = JsonNode.Parse(Fixtures.Read("getactiveplaylists-success.json"))!;
        ((JsonArray)node["Result"]!["RankedPlaylists"]!)[0]!["bCompetitive"] = true;

        ActivePlaylistsOutput output = ActivePlaylistsOutputMapper.Map(Deserialize(node), Names());

        Assert.True(output.RankedPlaylists[0].Competitive);
        JsonArray ranked = (JsonArray)JsonNode.Parse(JsonSerialize.ActivePlaylists(output))!["rankedPlaylists"]!;
        JsonNode first = ranked[0]!;
        Assert.True((bool)first["competitive"]!);
        // The b-prefixed wire key never reaches the tidy output.
        Assert.Null(first["bCompetitive"]);
    }

    [Fact]
    public void Map_NestedExtensionData_CamelCasedRecursively()
    {
        JsonNode node = JsonNode.Parse(Fixtures.Read("getactiveplaylists-success.json"))!;
        ((JsonArray)node["Result"]!["RankedPlaylists"]!)[0]!["DisparitySettings"] = new JsonObject
        {
            ["HighestMuNewPlayersCanHave"] = 10,
            ["HighestTierNewPlayersCanPlayWith"] = 5,
            ["MaximumRankDisparity"] = 3,
        };

        ActivePlaylistsOutput output = ActivePlaylistsOutputMapper.Map(Deserialize(node), Names());
        JsonArray ranked = (JsonArray)JsonNode.Parse(JsonSerialize.ActivePlaylists(output))!["rankedPlaylists"]!;
        JsonNode first = ranked[0]!;

        // The nested object's keys are camelCased at every level.
        JsonNode disparity = first["disparitySettings"]!;
        Assert.Equal(10, (int)disparity["highestMuNewPlayersCanHave"]!);
        Assert.Equal(5, (int)disparity["highestTierNewPlayersCanPlayWith"]!);
        Assert.Equal(3, (int)disparity["maximumRankDisparity"]!);
        Assert.Null(disparity["HighestMuNewPlayersCanHave"]);
    }
}
