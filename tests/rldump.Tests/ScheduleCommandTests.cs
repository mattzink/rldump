using System.Net;
using System.Text.Json.Nodes;
using Rldump.Cli;
using Rldump.Tests.Fakes;

namespace Rldump.Tests;

/// <summary>
/// End-to-end tests of the schedule command through RldumpApp (the same path
/// the CLI wiring uses), with a fake HTTP handler and a temp config file.
/// </summary>
public class ScheduleCommandTests
{
    private const string Token = "super-secret-token-abc";

    private static string ConfigJson() => "{ \"api\": { \"token\": \"" + Token + "\" } }";

    private static CliOptions Options(string configPath, bool raw = false, bool compact = false, string? outPath = null) =>
        new(configPath, raw, compact, outPath, 5);

    [Fact]
    public async Task Success_Tidy_Exit0_IndentedJson_EmptyStderr()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("getschedule-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("USW", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal("", console.StdErr.ToString());
        string json = console.StdOut.ToString();
        Assert.StartsWith("{\n", json);
        Assert.EndsWith("\n", json);
        Assert.DoesNotContain(Token, json);
        JsonNode node = JsonNode.Parse(json)!;
        JsonArray schedules = (JsonArray)node["schedules"]!;
        Assert.Equal(9, schedules.Count);
    }

    [Fact]
    public async Task Success_Compact_SingleLine()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("getschedule-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("USW", Options(configPath, compact: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        Assert.DoesNotContain("\n", json.TrimEnd('\n'));
        Assert.NotNull(JsonNode.Parse(json));
    }

    [Fact]
    public async Task Raw_PrintsFullEnvelope_Verbatim()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("getschedule-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("USW", Options(configPath, raw: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        string json = console.StdOut.ToString();
        JsonNode node = JsonNode.Parse(json)!;
        // The envelope wrapper is present, unlike in tidy mode.
        Assert.NotNull(node["Result"]);
        // Wire shape is preserved: PascalCase keys and wire-only fields.
        Assert.Contains("\"IsSchedulingTournament\"", json);
        Assert.Contains("\"ScheduleID\"", json);
        Assert.Contains("\"bUpdateSkill\"", json);
    }

    [Fact]
    public async Task OutFile_WritesFile_EmptyStdout()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        string outPath = System.IO.Path.Combine(dir.Path, "out.json");
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("getschedule-success.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("USW", Options(configPath, outPath: outPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal("", console.StdOut.ToString());
        string json = File.ReadAllText(outPath);
        JsonNode node = JsonNode.Parse(json)!;
        JsonArray schedules = (JsonArray)node["schedules"]!;
        Assert.Equal(9, schedules.Count);
    }

    [Fact]
    public async Task RegionIsPassedThroughInRequestBody()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("getschedule-success.json"));

        await RldumpApp.ScheduleAsync("XYZ", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal("""{"Region":"XYZ"}""", handler.Requests[0].Body);
    }

    [Fact]
    public async Task ApiErrorEnvelope_Exit1_StderrMessage_NoTokenLeak()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.JsonResponse(Fixtures.Read("getschedule-error.json"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("XYZ", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        string stderr = console.StdErr.ToString();
        Assert.Contains("API error (ScheduleRegionIsInvalid)", stderr);
        Assert.StartsWith("rldump:", stderr);
        Assert.DoesNotContain(Token, stderr);
        Assert.Equal("", console.StdOut.ToString());
    }

    [Fact]
    public async Task Non2xx_Exit1()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.Status(HttpStatusCode.InternalServerError, "boom");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("USW", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("HTTP 500", console.StdErr.ToString());
    }

    [Fact]
    public async Task NoConfigFile_Exit3()
    {
        var handler = FakeHttpHandler.JsonResponse("{}");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("USW", Options("/nonexistent/rldump.json"), handler, TestContext.Current.CancellationToken);

        Assert.Equal(3, code);
        Assert.Contains("rldump: no config file at", console.StdErr.ToString());
    }

    [Fact]
    public async Task ConfigWithoutToken_Exit3()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", "{ \"api\": { \"baseUrl\": \"https://example.test\" } }");
        var handler = FakeHttpHandler.JsonResponse("{}");
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("USW", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(3, code);
        Assert.Contains("no API token", console.StdErr.ToString());
    }

    [Fact]
    public async Task NetworkFailure_Exit4()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.Throw(new HttpRequestException("connection refused"));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("USW", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(4, code);
        Assert.Contains("request failed", console.StdErr.ToString());
    }

    [Fact]
    public async Task Timeout_Exit4()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = FakeHttpHandler.Throw(new TaskCanceledException("The operation was canceled."));
        using var console = new ConsoleCapture();

        int code = await RldumpApp.ScheduleAsync("USW", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(4, code);
        Assert.Contains("request timed out after 5s", console.StdErr.ToString());
    }
}
