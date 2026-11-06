using System.Text.Json.Nodes;
using Rldump.Cli;
using Rldump.Tests.Fakes;

namespace Rldump.Tests;

/// <summary>
/// End-to-end tests of the playlists command group. "all"
/// (Config/GetPlaylists) needs no value transform, so tidy mode passes the
/// Result payload through with recursively camelCased keys; "active"
/// (Playlists/GetActivePlaylists) keeps each entry's raw playlist index and
/// adds playlistName alongside, with a second Config/GetPlaylists call for
/// the names (tidy mode only). Raw mode prints the envelope verbatim and
/// skips the playlist lookup.
/// </summary>
public class PlaylistsCommandTests
{
    private const string Token = "playlists-token";

    private static string ConfigJson() => "{ \"api\": { \"token\": \"" + Token + "\" } }";

    private static CliOptions Options(string configPath, bool raw = false, bool compact = false) =>
        new(configPath, raw, compact, null, 5);

    /// <summary>A handler that serves the right fixture per endpoint.</summary>
    private static FakeHttpHandler EndpointHandler() =>
        FakeHttpHandler.ByUri(uri => uri.Contains("GetActivePlaylists")
            ? Fixtures.Read("getactiveplaylists-success.json")
            : Fixtures.Read("getplaylists-success.json"));

    // ===== playlists all =====

    [Fact]
    public async Task All_Tidy_Exit0_ResultPayload_CamelCasedKeys()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlaylistsAllAsync(Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal("", console.StdErr.ToString());
        // One call, empty body, to the catalog endpoint.
        Assert.Single(handler.Requests);
        Assert.Equal("{}", handler.Requests[0].Body);
        Assert.Contains("/Config/GetPlaylists", handler.Requests[0].Uri.ToString());

        // The Result payload, not the envelope; keys camelCased.
        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        Assert.Null(node["Result"]);
        Assert.Null(node["Playlists"]);
        JsonArray playlists = (JsonArray)node["playlists"]!;
        Assert.Equal(74, playlists.Count);
        // Fixture entry ID 1 (the first entry is ID 0, "Casual").
        Assert.Equal(1, (int)playlists[1]!["id"]!);
        Assert.Equal("Duel", (string?)playlists[1]!["title"]);
        Assert.Equal(2, (int)playlists[1]!["playerCount"]!);
        Assert.False((bool)playlists[1]!["hidden"]!);
        Assert.False((bool)playlists[1]!["ranked"]!);
        // Hidden playlists carry a null title; nulls pass through.
        JsonNode hidden = playlists.First(p => p!["id"]!.GetValue<int>() == 6)!;
        Assert.True((bool)hidden["hidden"]!);
        Assert.Null(hidden["title"]);
        Assert.DoesNotContain(Token, console.StdOut.ToString());
    }

    [Fact]
    public async Task All_Raw_Exit0_VerbatimEnvelope()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlaylistsAllAsync(Options(configPath, raw: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        JsonNode node = JsonNode.Parse(json)!;
        // The envelope wrapper is present, unlike in tidy mode.
        Assert.NotNull(node["Result"]);
        // Wire shape is preserved: PascalCase keys at every level.
        Assert.NotNull(node["Result"]!["Playlists"]);
        Assert.Contains("\"PlayerCount\"", json);
        Assert.Contains("\"Ranked\"", json);
    }

    [Fact]
    public async Task All_Compact_SingleLine()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlaylistsAllAsync(Options(configPath, compact: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        Assert.DoesNotContain("\n", json.TrimEnd('\n'));
        JsonNode node = JsonNode.Parse(json)!;
        Assert.Equal(74, ((JsonArray)node["playlists"]!).Count);
    }

    [Fact]
    public async Task All_ErrorEnvelope_Exit1_StderrMessage_NoTokenLeak()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(
            """{ "Error": { "Message": "The request body does not match this endpoint.", "Type": "RequestError" } }""");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlaylistsAllAsync(Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        string stderr = console.StdErr.ToString();
        Assert.Contains("API error (RequestError)", stderr);
        Assert.StartsWith("rldump:", stderr);
        Assert.DoesNotContain(Token, stderr);
        Assert.Equal("", console.StdOut.ToString());
    }

    // ===== playlists active =====

    [Fact]
    public async Task Active_Tidy_Exit0_TwoRequests_NamesAlongsideRawIndices()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlaylistsActiveAsync(Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal("", console.StdErr.ToString());
        // Step 1: the active-playlists lookup; step 2: the catalog lookup for
        // the names, only in tidy mode. Both take an empty body.
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("{}", handler.Requests[0].Body);
        Assert.Contains("/Playlists/GetActivePlaylists", handler.Requests[0].Uri.ToString());
        Assert.Equal("{}", handler.Requests[1].Body);
        Assert.Contains("/Config/GetPlaylists", handler.Requests[1].Uri.ToString());

        // Tidy output: the Result payload, not the envelope.
        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        Assert.Null(node["Result"]);
        JsonArray casual = (JsonArray)node["casualPlaylists"]!;
        JsonArray ranked = (JsonArray)node["rankedPlaylists"]!;
        Assert.Equal(7, casual.Count);
        Assert.Equal(8, ranked.Count);

        // Null start/end times are omitted; the raw index stays a number and
        // the name sits alongside it.
        JsonNode firstCasual = casual[0]!;
        Assert.Equal(1, (int)firstCasual["playlist"]!);
        Assert.Equal("Duel", (string?)firstCasual["playlistName"]);
        Assert.Null(firstCasual["startTime"]);
        Assert.Null(firstCasual["endTime"]);
        Assert.Equal("OnesCasual", (string?)firstCasual["nodeID"]);

        // Present times are ISO 8601 UTC (fixture: 1793811600 / 1794416400).
        JsonNode largeParty = casual[4]!;
        Assert.Equal("2026-11-04T17:00:00Z", (string?)largeParty["startTime"]);
        Assert.Equal("2026-11-11T17:00:00Z", (string?)largeParty["endTime"]);

        // Ranked entries: 10 → "Duel", a start but no end (null → omitted).
        JsonNode firstRanked = ranked[0]!;
        Assert.Equal(10, (int)firstRanked["playlist"]!);
        Assert.Equal("Duel", (string?)firstRanked["playlistName"]);
        Assert.Equal("2026-09-23T16:00:00Z", (string?)firstRanked["startTime"]);
        Assert.Null(firstRanked["endTime"]);

        // Pass-through fields keep their values with camelCased keys,
        // recursively (rankedUnlock's nested keys too).
        Assert.Equal(20, (int)node["xpLevelUnlocked"]!);
        Assert.Equal("Challenges", (string?)node["rankedUnlock"]!["criteria"]);
        Assert.Equal([8, 9, 10, 11], ((JsonArray)node["rankedUnlock"]!["value"]!).Select(v => v!.GetValue<int>()).ToArray());

        Assert.DoesNotContain(Token, console.StdOut.ToString());
    }

    [Fact]
    public async Task Active_Raw_Exit0_OneRequest_Verbatim()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlaylistsActiveAsync(Options(configPath, raw: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        JsonNode node = JsonNode.Parse(json)!;
        // The GetActivePlaylists envelope (the catalog lookup is skipped in
        // raw mode — no transform, so no names).
        Assert.Single(handler.Requests);
        Assert.NotNull(node["Result"]);
        Assert.NotNull(node["Result"]!["CasualPlaylists"]);
        // Wire shape is preserved: PascalCase keys at every level.
        Assert.Contains("\"NodeID\"", json);
        Assert.Contains("\"XPLevelUnlocked\"", json);
    }

    [Fact]
    public async Task Active_Compact_SingleLine()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlaylistsActiveAsync(Options(configPath, compact: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        Assert.DoesNotContain("\n", json.TrimEnd('\n'));
        JsonNode node = JsonNode.Parse(json)!;
        Assert.Equal("Duel", (string?)node["casualPlaylists"]![0]!["playlistName"]);
    }

    [Fact]
    public async Task Active_ActiveQueryError_Exit1_NoPlaylistRequest()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.ByUri(uri => uri.Contains("GetActivePlaylists")
            ? """{ "Error": { "Message": "no active playlists", "Type": "NotFound" } }"""
            : Fixtures.Read("getplaylists-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlaylistsActiveAsync(Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        // The flow stops at the failed lookup; the catalog endpoint is never
        // called.
        Assert.Single(handler.Requests);
        Assert.Contains("API error (NotFound): no active playlists", console.StdErr.ToString());
        Assert.Equal("", console.StdOut.ToString());
    }

    [Fact]
    public async Task Active_PlaylistsQueryError_Exit1_StderrMessage()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.ByUri(uri => uri.Contains("GetPlaylists")
            ? """{ "Error": { "Message": "no playlists", "Type": "NotFound" } }"""
            : Fixtures.Read("getactiveplaylists-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlaylistsActiveAsync(Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("API error (NotFound): no playlists", console.StdErr.ToString());
        Assert.Equal("", console.StdOut.ToString());
    }
}
