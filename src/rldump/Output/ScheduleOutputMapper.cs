using System.Globalization;
using System.Text.Json;
using Rldump.Api;

namespace Rldump.Output;

/// <summary>
/// Maps the GetSchedule API result to the tidy output model:
/// IDs → strings, epoch seconds → ISO 8601 UTC, bX → x, mode indices → names,
/// and extension data passed through with camelCased keys.
/// </summary>
internal static class ScheduleOutputMapper
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static ScheduleOutput Map(GetScheduleResult result, IReadOnlyDictionary<int, string> modeNames)
    {
        return new ScheduleOutput
        {
            Schedules = result.Schedules.Select(entry => MapSchedule(entry, modeNames)).ToArray(),
            ExtensionData = CamelCaseKeys(result.ExtensionData),
        };
    }

    private static ScheduleEntryOut MapSchedule(ScheduleEntry entry, IReadOnlyDictionary<int, string> modeNames)
    {
        return new ScheduleEntryOut
        {
            Time = ToIso8601Utc(entry.Time),
            ScheduleId = entry.ScheduleID.ToString(Invariant),
            UpdateSkill = entry.bUpdateSkill,
            Tournaments = entry.Tournaments.Select(t => MapTournament(t, modeNames)).ToArray(),
            ExtensionData = CamelCaseKeys(entry.ExtensionData),
        };
    }

    private static TourSettingsOut MapTournament(TourSettings t, IReadOnlyDictionary<int, string> modeNames)
    {
        return new TourSettingsOut
        {
            Id = t.ID.ToString(Invariant),
            ScheduleId = t.ScheduleID.ToString(Invariant),
            StartTime = ToIso8601Utc(t.StartTime),
            GenerateBracketTime = t.GenerateBracketTime is long g ? ToIso8601Utc(g) : null,
            MatchExpireTimestamp = t.MatchExpireTimestamp is long m ? ToIso8601Utc(m) : null,
            Public = t.bPublic,
            IsMultiGameMode = t.bIsMultiGameMode,
            GameMode = ToModeName(t.GameMode, modeNames),
            GameModes = t.GameModes.Select(index => ToModeName(index, modeNames)).ToArray(),
            ExtensionData = CamelCaseKeys(t.ExtensionData),
        };
    }

    private static GameModeName ToModeName(int index, IReadOnlyDictionary<int, string> modeNames) =>
        modeNames.TryGetValue(index, out string? name)
            ? new GameModeName(index, name)
            : new GameModeName(index, null);

    /// <summary>Unix seconds → "yyyy-MM-ddTHH:mm:ssZ" (UTC), without DateTime round-trips.</summary>
    private static string ToIso8601Utc(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Invariant);

    /// <summary>
    /// Copies extension data, camelCasing each key. The serializer writes
    /// extension-data keys verbatim (the naming policy does not apply to them),
    /// so this must happen here. CamelCase is idempotent on already-camelCased
    /// names, so keys that were added by the API in either casing come out
    /// consistently.
    /// </summary>
    private static Dictionary<string, JsonElement>? CamelCaseKeys(Dictionary<string, JsonElement>? data)
    {
        if (data is null)
            return null;

        Dictionary<string, JsonElement> result = new(data.Count);
        foreach ((string key, JsonElement value) in data)
            result[System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(key)] = value;
        return result;
    }
}
