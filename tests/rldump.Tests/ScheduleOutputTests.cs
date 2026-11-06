using System.Text.Json;
using System.Text.Json.Nodes;
using Rldump.Api;
using Rldump.Output;

namespace Rldump.Tests;

/// <summary>
/// Fixture-driven tests of the tidy transformation. The success fixture is a
/// verbatim copy of a real API response — do not edit it; it is the ground
/// truth for the wire shape.
/// </summary>
public class ScheduleOutputTests
{
    private static readonly GameModeMap Modes = GameModeMap.Load();

    private static GetScheduleResult LoadFixture() =>
        JsonSerializer.Deserialize(Fixtures.Read("getschedule-success.json"), ApiJsonContext.Default.EnvelopeGetScheduleResult)!.Result!;

    [Fact]
    public void Map_Fixture_ProducesExpectedTidyJson()
    {
        ScheduleOutput output = ScheduleOutputMapper.Map(LoadFixture(), Modes.Modes);
        string actual = JsonSerialize.Schedule(output);

        string expected = Fixtures.Read("getschedule-tidy-expected.json");
        Assert.Equal(JsonNode.Parse(expected)!.ToJsonString(), JsonNode.Parse(actual)!.ToJsonString());
    }

    [Fact]
    public void Map_KnownModes_AddNamesAlongsideRawIndices()
    {
        ScheduleOutput output = ScheduleOutputMapper.Map(LoadFixture(), Modes.Modes);

        // First fixture tournament: 3v3 Dropshot, GameMode 5, no sub-modes.
        TourSettingsOut first = output.Schedules[0].Tournaments[0];
        Assert.Equal(5, first.GameMode);
        Assert.Equal("Dropshot", first.GameModeName);
        Assert.Empty(first.GameModes);
        Assert.Empty(first.GameModeNames);

        // A Pentathlon tournament: GameMode 27 with GameModes [3, 12, 20, 1, 6].
        // The fixture has four Pentathlon tournaments, so select the first with
        // this exact sub-mode sequence (order is preserved by the mapper).
        TourSettingsOut pentathlon = output.Schedules
            .SelectMany(s => s.Tournaments)
            .First(t => t.GameMode == 27
                && t.GameModes.SequenceEqual([3, 12, 20, 1, 6]));
        Assert.Equal("Pentathlon Tournament", pentathlon.GameModeName);
        Assert.Equal(
            ["Rumble", "Dropshot Rumble", "Super Cube", "Hoops", "Heatseeker"],
            pentathlon.GameModeNames);
    }

    [Fact]
    public void Map_IdsAreStrings_TimestampsAreIso8601Utc()
    {
        ScheduleOutput output = ScheduleOutputMapper.Map(LoadFixture(), Modes.Modes);
        ScheduleEntryOut schedule = output.Schedules[0];

        Assert.Equal("59935", schedule.ScheduleId);
        Assert.Equal("2026-11-04T21:00:00Z", schedule.Time);

        TourSettingsOut tournament = schedule.Tournaments[0];
        Assert.Equal("53008201", tournament.Id);
        Assert.Equal("2026-11-04T21:00:00Z", tournament.StartTime);
        Assert.False(tournament.Public);
    }

    [Fact]
    public void Map_UnknownModeIndices_StayRawNumbers_WithNameUnknown()
    {
        JsonNode node = JsonNode.Parse(Fixtures.Read("getschedule-success.json"))!;
        JsonObject tour = (JsonObject)node["Result"]!["Schedules"]![0]!["Tournaments"]![0]!;
        tour["GameMode"] = 4;
        tour["GameModes"] = new JsonArray(999);

        GetScheduleResult result = Deserialize(node);
        string json = JsonSerialize.Schedule(ScheduleOutputMapper.Map(result, Modes.Modes));
        JsonObject outTour = (JsonObject)JsonNode.Parse(json)!["schedules"]![0]!["tournaments"]![0]!;

        // Unknown indices stay raw JSON numbers (GetValue<int> would throw if a
        // string had been written), and their names come out "Unknown".
        Assert.Equal(4, outTour["gameMode"]!.GetValue<int>());
        Assert.Equal("Unknown", (string?)outTour["gameModeName"]!);
        JsonArray modes = (JsonArray)outTour["gameModes"]!;
        Assert.Single(modes);
        Assert.Equal(999, modes[0]!.GetValue<int>());
        JsonArray modeNames = (JsonArray)outTour["gameModeNames"]!;
        Assert.Equal("Unknown", (string?)modeNames[0]!);
    }

    [Fact]
    public void Map_ExtensionData_PassesThroughCamelCasedWithoutDuplicates()
    {
        JsonNode node = JsonNode.Parse(Fixtures.Read("getschedule-success.json"))!;
        ((JsonObject)node["Result"]!["Schedules"]![0]!["Tournaments"]![0]!)["BracketsUrl"] = "https://example.test/brackets/1";
        ((JsonObject)node["Result"]!["Schedules"]![0]!)["Season"] = 12;

        GetScheduleResult result = Deserialize(node);
        string json = Indented(JsonSerialize.Schedule(ScheduleOutputMapper.Map(result, Modes.Modes)));

        Assert.Contains("\"bracketsUrl\": \"https://example.test/brackets/1\"", json);
        Assert.Contains("\"season\": 12", json);
        // 9 schedules × 2 tournaments, each with exactly one "title" key:
        // no key is emitted twice.
        Assert.Equal(18, System.Text.RegularExpressions.Regex.Count(json, "\"title\": "));
    }

    [Fact]
    public void Map_AbsentFields_StayAbsent_NullTransformedFieldsOmitted()
    {
        ScheduleOutput output = ScheduleOutputMapper.Map(LoadFixture(), Modes.Modes);
        string json = Indented(JsonSerialize.Schedule(output));

        // Absent on the wire → absent in the output (no invented nulls).
        Assert.DoesNotContain("regionName", json);
        Assert.DoesNotContain("matchExpireTimestamp", json);
        Assert.DoesNotContain("minPlayersPerTeam", json);
        Assert.DoesNotContain("description", json);

        // Present as null, transformed nullable field → omitted.
        Assert.DoesNotContain("generateBracketTime", json);

        // Present as null, pass-through field → preserved verbatim.
        Assert.Contains("\"mapSetName\": null", json);
    }

    private static GetScheduleResult Deserialize(JsonNode node) =>
        JsonSerializer.Deserialize(node.ToJsonString(), ApiJsonContext.Default.EnvelopeGetScheduleResult)!.Result!;

    /// <summary>
    /// Re-renders the compact serializer output as indented JSON so content
    /// assertions can read naturally ("key": value). Re-parsing through
    /// <see cref="JsonNode"/> preserves property order and values exactly, so
    /// this is safe for substring and regex checks.
    /// </summary>
    private static string Indented(string compact) =>
        JsonNode.Parse(compact)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
}
