using System.Text.Json.Nodes;
using Rldump.Cli;
using Rldump.Tests.Fakes;

namespace Rldump.Tests;

public class ClubStatsCommandTests
{
    private const string Token = "club-stats-token";

    private static string ConfigJson() => "{ \"api\": { \"token\": \"" + Token + "\" } }";

    private static CliOptions Options(string configPath, bool raw = false, bool compact = false) =>
        new(configPath, raw, compact, null, 5);

    private static FakeHttpHandler EndpointHandler() =>
        FakeHttpHandler.JsonResponse(Fixtures.Read("getclubstats-success.json"));

    [Fact]
    public async Task Tidy_SendsClubId_AndCamelCasesStats()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ClubStatsAsync(156045, Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Single(handler.Requests);
        Assert.Equal("{\"ClubID\":156045}", handler.Requests[0].Body);
        Assert.Contains("/Clubs/GetStats", handler.Requests[0].Uri.ToString());
        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        Assert.Null(node["Result"]);
        Assert.Equal(9293, (int)node["careerStats"]!["goal"]!);
        Assert.Equal(5, ((JsonArray)node["previousSeasonalBadges"]!).Count);
        Assert.Equal(2, (int)node["seasonalStats"]![0]!["badge"]!);
        Assert.Equal("Club_S24_M1", (string?)node["seasonalTitles"]![0]!["title"]);
        Assert.DoesNotContain(Token, console.StdOut.ToString());
    }

    [Fact]
    public async Task Raw_PrintsFullEnvelopeVerbatim()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ClubStatsAsync(156045, Options(configPath, raw: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Single(handler.Requests);
        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        Assert.NotNull(node["Result"]!["CareerStats"]);
        Assert.Contains("\"CareerStats\"", console.StdOut.ToString());
    }
}