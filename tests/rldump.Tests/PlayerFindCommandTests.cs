using System.Text.Json.Nodes;
using Rldump.Cli;
using Rldump.Tests.Fakes;

namespace Rldump.Tests;

/// <summary>
/// End-to-end tests of the player find command (GraphQL/AccountQuery). No
/// field needs a value transform, so tidy mode passes the Result payload
/// through with recursively camelCased keys and raw mode the envelope.
/// </summary>
public class PlayerFindCommandTests
{
    private const string Token = "player-find-token";

    private static string ConfigJson() => "{ \"api\": { \"token\": \"" + Token + "\" } }";

    private static CliOptions Options(string configPath, bool raw = false, bool compact = false, string? outPath = null) =>
        new(configPath, raw, compact, outPath, 5);

    [Fact]
    public async Task Success_Tidy_Exit0_CamelCasedKeys_Recursively()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("accountquery-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerFindAsync("Z1nky", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal("", console.StdErr.ToString());
        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        // The Result payload, not the envelope; keys camelCased.
        Assert.Null(node["Result"]);
        Assert.Equal("15ca5c7db5b44f9f9a50e3f410a889ef", (string?)node["accountID"]);
        Assert.Equal("Z1nky", (string?)node["displayName"]);
        Assert.Equal("Epic Games", (string?)node["identityProvider"]);
        // Nested objects are camelCased too; nulls pass through.
        JsonArray linked = (JsonArray)node["linkedAccounts"]!;
        Assert.Equal(3, linked.Count);
        Assert.Equal("636d203f5595e2f6", (string?)linked[0]!["accountID"]);
        Assert.Null(linked[0]!["displayName"]);
        Assert.Equal("Nintendo", (string?)linked[0]!["identityProvider"]);
        // No PascalCase wire keys leak into tidy output.
        Assert.Null(node["AccountID"]);
        Assert.DoesNotContain(Token, console.StdOut.ToString());
    }

    [Fact]
    public async Task Raw_PrintsFullEnvelope_Verbatim()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("accountquery-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerFindAsync("Z1nky", Options(configPath, raw: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        JsonNode node = JsonNode.Parse(json)!;
        // The envelope wrapper is present, unlike in tidy mode.
        Assert.NotNull(node["Result"]);
        // Wire shape is preserved: PascalCase keys at every level.
        Assert.NotNull(node["Result"]!["AccountID"]);
        Assert.Contains("\"LinkedAccounts\"", json);
        Assert.Contains("\"IdentityProvider\"", json);
    }

    [Fact]
    public async Task Success_Compact_SingleLine()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("accountquery-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerFindAsync("Z1nky", Options(configPath, compact: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        Assert.DoesNotContain("\n", json.TrimEnd('\n'));
        Assert.NotNull(JsonNode.Parse(json));
    }

    [Fact]
    public async Task OutFile_WritesFile_EmptyStdout()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        string outPath = System.IO.Path.Combine(dir.Path, "out.json");
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("accountquery-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerFindAsync("Z1nky", Options(configPath, outPath: outPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal("", console.StdOut.ToString());
        JsonNode node = JsonNode.Parse(File.ReadAllText(outPath))!;
        Assert.Equal("Z1nky", (string?)node["displayName"]);
    }

    [Fact]
    public async Task SendsUserInRequestBody()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("accountquery-success.json"));

        await RldumpApp.PlayerFindAsync("Z1nky", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal("""{"User":"Z1nky"}""", handler.Requests[0].Body);
        Assert.Contains("/GraphQL/AccountQuery", handler.Requests[0].Uri.ToString());
    }

    [Fact]
    public async Task EpicPlayerId_QueriesAccountById()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("accountquery-success.json"));

        await RldumpApp.PlayerFindAsync("Epic|15ca5c7db5b44f9f9a50e3f410a889ef|0", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Single(handler.Requests);
        Assert.Equal("""{"User":"15ca5c7db5b44f9f9a50e3f410a889ef"}""", handler.Requests[0].Body);
    }

    [Fact]
    public async Task ErrorEnvelope_Exit1_StderrMessage_NoTokenLeak()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(
            """{ "Error": { "Message": "The request body does not match this endpoint.", "Type": "RequestError" } }""");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerFindAsync("ab", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        string stderr = console.StdErr.ToString();
        Assert.Contains("API error (RequestError)", stderr);
        Assert.StartsWith("rldump:", stderr);
        Assert.DoesNotContain(Token, stderr);
        Assert.Equal("", console.StdOut.ToString());
    }

    [Fact]
    public async Task EmptyErrorEnvelope_Exit1_NormalizedMessage()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        // AccountQuery answers unknown names with a bare {"Error":{}}.
        var handler = FakeHttpHandler.JsonResponse("""{ "Error": {} }""");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerFindAsync("qqqzzz999", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        string stderr = console.StdErr.ToString();
        Assert.Contains("API error (Unknown): the API returned an error with no details", stderr);
        Assert.Equal("", console.StdOut.ToString());
    }
}
