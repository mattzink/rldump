using System.Text.Json.Nodes;
using Rldump.Cli;
using Rldump.Tests.Fakes;

namespace Rldump.Tests;

/// <summary>
/// End-to-end tests of the regions command. The API marks the result opaque,
/// so tidy mode prints the Result payload verbatim and raw mode the envelope.
/// </summary>
public class RegionsCommandTests
{
    private const string Token = "regions-token";

    private static string ConfigJson() => "{ \"api\": { \"token\": \"" + Token + "\" } }";

    private static CliOptions Options(string configPath, bool raw = false, bool compact = false) =>
        new(configPath, raw, compact, null, 5);

    [Fact]
    public async Task Success_Tidy_PrintsResultPayloadOnly()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse("{ \"Result\": { \"Regions\": [\"USW\", \"USE\", \"EU\"] } }");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.RegionsAsync(Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        // The Result payload, not the envelope.
        Assert.NotNull(node["Regions"]);
        Assert.Null(node["Result"]);
        Assert.Equal(3, ((JsonArray)node["Regions"]!).Count);
    }

    [Fact]
    public async Task Raw_PrintsFullEnvelope()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse("{ \"Result\": { \"Regions\": [\"USW\"] } }");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.RegionsAsync(Options(configPath, raw: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        Assert.NotNull(node["Result"]);
        JsonArray regions = (JsonArray)node["Result"]!["Regions"]!;
        Assert.Single(regions);
    }

    [Fact]
    public async Task Compact_SingleLine()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse("{ \"Result\": { \"Regions\": [\"USW\"] } }");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.RegionsAsync(Options(configPath, compact: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        Assert.DoesNotContain("\n", json.TrimEnd('\n'));
    }

    [Fact]
    public async Task ErrorEnvelope_Exit1()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse("{ \"Error\": { \"Message\": \"not logged in\", \"Type\": \"NotLoggedIn\" } }");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.RegionsAsync(Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("API error (NotLoggedIn)", console.StdErr.ToString());
    }

    [Fact]
    public async Task SendsEmptyObjectBody()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse("{ \"Result\": {} }");

        await RldumpApp.RegionsAsync(Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal("{}", handler.Requests[0].Body);
        Assert.Contains("/Config/GetScheduleRegions", handler.Requests[0].Uri.ToString());
    }
}
