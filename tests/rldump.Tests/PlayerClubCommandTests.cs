using System.Text.Json.Nodes;
using Rldump.Api;
using Rldump.Cli;
using Rldump.Output;
using Rldump.Tests.Fakes;

namespace Rldump.Tests;

public class PlayerClubCommandTests
{
    private const string Token = "player-club-token";
    private const string AccountId = "15ca5c7db5b44f9f9a50e3f410a889ef";
    private static string ConfigJson() => "{ \"api\": { \"token\": \"" + Token + "\" } }";

    private static CliOptions Options(string configPath, bool raw = false, bool compact = false) =>
        new(configPath, raw, compact, null, 5);

    private static FakeHttpHandler EndpointHandler() =>
        FakeHttpHandler.ByUri(uri => uri.Contains("GetPlayerClubDetails")
        ? Fixtures.Read("getplayerclubdetails-success.json")
            : Fixtures.Read("accountquery-success.json"));

    [Fact]
    public async Task Success_Tidy_ResolvesPlayer_AndMapsLiveFixture()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerClubAsync("Z1nky", Options(configPath), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Equal("", console.StdErr.ToString());
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("""{"User":"Z1nky"}""", handler.Requests[0].Body);
        Assert.Contains("/GraphQL/AccountQuery", handler.Requests[0].Uri.ToString());
        Assert.Equal("{\"PlayerID\":\"Epic|" + AccountId + "|0\"}", handler.Requests[1].Body);
        Assert.Contains("/Clubs/GetPlayerClubDetails", handler.Requests[1].Uri.ToString());

        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        JsonNode club = node["clubDetails"]!;
        Assert.Equal("156045", (string?)club["clubID"]);
        Assert.Equal(-52556, (int)club["primaryColor"]!);
        Assert.Equal("#FF32B4", (string?)club["primaryHexColor"]);
        Assert.Equal(-13435004, (int)club["accentColor"]!);
        Assert.Equal("#32FF84", (string?)club["accentHexColor"]);
        Assert.Equal("2026-10-21T22:09:02Z", (string?)club["lastUpdatedTime"]);
        Assert.Equal("2018-08-30T02:22:58Z", (string?)club["createdTime"]);
        Assert.Null(club["deletedTime"]);
        Assert.Equal("Sons of a Motherless Goat", (string?)club["clubName"]);
        Assert.Equal("SONS", (string?)club["clubTag"]);
        Assert.False((bool)club["verified"]!);
        JsonArray members = (JsonArray)club["members"]!;
        Assert.Equal(10, members.Count);
        Assert.Equal("Z1nky", (string?)members[1]!["playerName"]);
        Assert.Equal("2018-08-30T02:22:58Z", (string?)members[1]!["createdTime"]);
        Assert.Null(members[1]!["deletedTime"]);
        Assert.DoesNotContain("bVerified", console.StdOut.ToString());
        Assert.DoesNotContain(Token, console.StdOut.ToString());
    }

      [Fact]
      public void Mapper_StringifiesClubIdAboveJavaScriptSafeIntegerRange()
      {
        GetPlayerClubDetailsResult result = new()
        {
          ClubDetails = new ClubDetails { ClubID = 9007199254740993 },
        };

        ClubDetailsOutput output = PlayerClubOutputMapper.Map(result).ClubDetails;

        Assert.Equal("9007199254740993", output.ClubID);
      }

      [Fact]
      public void Mapper_EncodesNonOpaqueColorWithCssAlphaOrder()
      {
        GetPlayerClubDetailsResult result = new()
        {
          ClubDetails = new ClubDetails { PrimaryColor = unchecked((int)0x80112233) },
        };

        ClubDetailsOutput output = PlayerClubOutputMapper.Map(result).ClubDetails;

        Assert.Equal("#11223380", output.PrimaryHexColor);
      }

    [Fact]
    public async Task Raw_PrintsClubEnvelope_Verbatim()
    {
        using var dir = new TempDir();
        string configPath = dir.WriteConfig("rldump.json", ConfigJson());
        var handler = EndpointHandler();
        using var console = new ConsoleCapture();

        int code = await RldumpApp.PlayerClubAsync("Z1nky", Options(configPath, raw: true), handler, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        JsonNode node = JsonNode.Parse(console.StdOut.ToString())!;
        Assert.Equal(156045, (int)node["Result"]!["ClubDetails"]!["ClubID"]!);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("\"ClubName\": \"Sons of a Motherless Goat\"", console.StdOut.ToString());
        Assert.Contains("\"bVerified\": false", console.StdOut.ToString());
    }
}