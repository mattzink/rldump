using System.Text.Json.Nodes;
using Rldump.Api;
using Rldump.Cli;
using Rldump.Output;
using Rldump.Tests.Fakes;

namespace Rldump.Tests;

/// <summary>
/// End-to-end tests of the player skills command: the name is resolved to an
/// account ID via GraphQL/AccountQuery, then Skills/GetPlayerSkill is called
/// with PlayerID "Epic|&lt;account ID&gt;|0", then Config/GetPlaylists names
/// the skill playlists (tidy mode only). Tidy output keeps the raw playlist
/// indices and adds playlistName alongside ("Unknown" when unmapped); raw
/// mode prints the GetPlayerSkill envelope verbatim (two calls).
/// </summary>
public class PlayerSkillsCommandTests
{
    private const string Token = "player-skills-token";
    private const string AccountId = "15ca5c7db5b44f9f9a50e3f410a889ef";

    private static string ConfigJson() => "{ \"api\": { \"token\": \"" + Token + "\" } }";

    private static CliOptions Options(string configPath, bool raw = false, bool compact = false) =>
        new(configPath, raw, compact, null, 5);

    /// <summary>A handler that serves the right fixture per endpoint.</summary>
    private static FakeHttpHandler EndpointHandler() =>
        FakeHttpHandler.ByUri(uri => uri.Contains("GetPlayerSkill")
            ? Fixtures.Read("getplayerskill-success.json")
            : uri.Contains("GetPlaylists")
                ? Fixtures.Read("getplaylists-success.json")
                : Fixtures.Read("accountquery-success.json"));

    [Fact]
    public async Task Success_Tidy_Exit0_ThreeRequests_PlaylistNamesAlongsideRawIndices()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerSkillsAsync("Z1nky", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal("", console.StdErr.ToString());

        // Step 1: the account lookup, by name.
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("""{"User":"Z1nky"}""", handler.Requests[0].Body);
        Assert.Contains("/GraphQL/AccountQuery", handler.Requests[0].Uri.ToString());
        // Step 2: the skill lookup, with the resolved account ID.
        Assert.Equal("{\"PlayerID\":\"Epic|" + AccountId + "|0\"}", handler.Requests[1].Body);
        Assert.Contains("/Skills/GetPlayerSkill", handler.Requests[1].Uri.ToString());
        // Step 3: the playlist lookup (empty body), only needed for tidy mode.
        Assert.Equal("{}", handler.Requests[2].Body);
        Assert.Contains("/Config/GetPlaylists", handler.Requests[2].Uri.ToString());

        // Tidy output: the skill Result payload. Raw playlist indices are kept
        // and the names from getplaylists-success.json sit alongside them.
        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        Assert.Null(node["Result"]);
        JsonArray skills = (JsonArray)node["skills"]!;
        Assert.Equal(12, skills.Count);

        // Fixture order: 10, 11, 12, 13, 27, 28, 29, 30, 34, 61, 63, 0.
        // 12 is absent from the playlists fixture, so it maps to "Unknown".
        int[] expectedIds = [10, 11, 12, 13, 27, 28, 29, 30, 34, 61, 63, 0];
        string[] expectedNames = ["Duel", "Doubles", "Unknown", "Standard", "Hoops", "Rumble", "Dropshot", "Snow Day", "Tournament Match", "Quads", "Heatseeker", "Casual"];
        for (int i = 0; i < skills.Count; i++)
        {
            Assert.Equal(expectedIds[i], skills[i]!["playlist"]!.GetValue<int>());
            Assert.Equal(expectedNames[i], (string?)skills[i]!["playlistName"]!);
        }

        Assert.Equal(1086, (int)skills[3]!["rating"]!);
        Assert.Equal(49.2937, (double)skills[3]!["mu"]!, precision: 4);
        Assert.Equal(16, (int)skills[3]!["tier"]!);
        Assert.Equal("Champion I", (string?)skills[3]!["tierName"]);

        // Pass-through fields keep their values with camelCased keys.
        Assert.Equal(41.9739, (double)skills[1]!["mmr"]!, precision: 4);
        Assert.Equal(-1, (int)skills[1]!["winStreak"]!);
        Assert.Equal(5, (int)node["rewardLevels"]!["seasonLevel"]!);
        Assert.Equal(4, (int)node["rewardLevels"]!["seasonLevelWins"]!);
        Assert.DoesNotContain(Token, console.StdOut.ToString());
    }

    [Fact]
    public void Rating_ConvertsMuToDisplayedWholeNumber()
    {
        GetPlayerSkillResult result = new()
        {
            Skills = [new SkillEntry { Playlist = 13, Mu = 50.3191 }],
        };

        PlayerSkillOutput output = PlayerSkillOutputMapper.Map(result, new Dictionary<int, string>());

        Assert.Equal(50.3191, output.Skills[0].Mu!.Value, precision: 4);
        Assert.Equal(1106, output.Skills[0].Rating);
    }

    [Fact]
    public void TierName_MapsRocketLeagueRankTiers()
    {
        int[] tiers = [0, 1, 12, 13, 16, 19, 21, 22, 23];
        string[] names = [
            "Unranked", "Bronze I", "Platinum III", "Diamond I", "Champion I",
            "Grand Champion I", "Grand Champion III", "Supersonic Legend", "Unknown",
        ];
        GetPlayerSkillResult result = new()
        {
            Skills = tiers.Select(tier => new SkillEntry { Tier = tier }).ToList(),
        };

        PlayerSkillOutput output = PlayerSkillOutputMapper.Map(result, new Dictionary<int, string>());

        Assert.Equal(tiers, output.Skills.Select(skill => skill.Tier!.Value).ToArray());
        Assert.Equal(names, output.Skills.Select(skill => skill.TierName).ToArray());
    }

    [Fact]
    public async Task Raw_PrintsSkillEnvelope_Verbatim_TwoRequests()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerSkillsAsync("Z1nky", Options(configPath, raw: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        JsonNode node = JsonNode.Parse(json)!;
        // The GetPlayerSkill envelope (not the account lookup's).
        Assert.NotNull(node["Result"]);
        Assert.NotNull(node["Result"]!["Skills"]);
        // The playlist lookup is skipped in raw mode (no transform).
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain(handler.Requests, r => r.Uri.ToString().Contains("GetPlaylists"));
        // Wire shape preserved: PascalCase keys at every level.
        Assert.Contains("\"RewardLevels\"", json);
        Assert.Contains("\"PlacementMatchesPlayed\"", json);
    }

    [Fact]
    public async Task Success_Compact_SingleLine()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerSkillsAsync("Z1nky", Options(configPath, compact: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        Assert.DoesNotContain("\n", json.TrimEnd('\n'));
        JsonNode node = JsonNode.Parse(json)!;
        Assert.Equal("Duel", (string?)node["skills"]![0]!["playlistName"]!);
    }

    [Fact]
    public async Task EpicPlayerId_SkipsAccountLookup()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();

        int code = await RldumpApp.PlayerSkillsAsync("Epic|" + AccountId + "|0", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("{\"PlayerID\":\"Epic|" + AccountId + "|0\"}", handler.Requests[0].Body);
        Assert.Contains("/Skills/GetPlayerSkill", handler.Requests[0].Uri.ToString());
        Assert.Contains("/Config/GetPlaylists", handler.Requests[1].Uri.ToString());
    }

    [Fact]
    public async Task AccountQueryError_Exit1_NoSkillRequest()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse("""{ "Error": {} }""");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerSkillsAsync("qqqzzz999", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        // The flow stops at the failed lookup; the skill and playlist
        // endpoints are never called.
        Assert.Single(handler.Requests);
        string stderr = console.StdErr.ToString();
        Assert.Contains("API error (Unknown): the API returned an error with no details", stderr);
        Assert.DoesNotContain(Token, stderr);
        Assert.Equal("", console.StdOut.ToString());
    }

    [Fact]
    public async Task SkillQueryError_Exit1_NoPlaylistRequest()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.ByUri(uri => uri.Contains("GetPlayerSkill")
            ? """{ "Error": { "Message": "no skill data", "Type": "NotFound" } }"""
            : Fixtures.Read("accountquery-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerSkillsAsync("Z1nky", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        // The flow stops at the failed skill lookup; the playlist endpoint is
        // never called.
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("API error (NotFound): no skill data", console.StdErr.ToString());
        Assert.Equal("", console.StdOut.ToString());
    }

    [Fact]
    public async Task PlaylistsQueryError_Exit1_StderrMessage()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.ByUri(uri => uri.Contains("GetPlaylists")
            ? """{ "Error": { "Message": "no playlists", "Type": "NotFound" } }"""
            : uri.Contains("GetPlayerSkill")
                ? Fixtures.Read("getplayerskill-success.json")
                : Fixtures.Read("accountquery-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerSkillsAsync("Z1nky", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("API error (NotFound): no playlists", console.StdErr.ToString());
        Assert.Equal("", console.StdOut.ToString());
    }
}
