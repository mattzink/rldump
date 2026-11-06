using System.Globalization;
using Rldump.Api;

namespace Rldump.Output;

/// <summary>
/// Maps the GetSchedule API result to the tidy output model:
/// IDs → strings, epoch seconds → ISO 8601 UTC, bX → x, mode names added
/// alongside the raw indices, and extension data passed through with
/// camelCased keys.
/// </summary>
internal static class ScheduleOutputMapper
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static ScheduleOutput Map(GetScheduleResult result, IReadOnlyDictionary<int, string> modeNames)
    {
        return new ScheduleOutput
        {
            Schedules = result.Schedules.Select(entry => MapSchedule(entry, modeNames)).ToArray(),
            ExtensionData = TidyJson.CamelCaseKeys(result.ExtensionData),
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
            ExtensionData = TidyJson.CamelCaseKeys(entry.ExtensionData),
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
            GameMode = t.GameMode,
            GameModeName = ToModeName(t.GameMode, modeNames),
            GameModes = t.GameModes,
            GameModeNames = t.GameModes.Select(index => ToModeName(index, modeNames)).ToArray(),
            ExtensionData = TidyJson.CamelCaseKeys(t.ExtensionData),
        };
    }

    /// <summary>The mode name when the index is in the mapping, otherwise "Unknown".</summary>
    private static string ToModeName(int index, IReadOnlyDictionary<int, string> modeNames) =>
        modeNames.TryGetValue(index, out string? name) ? name! : "Unknown";

    /// <summary>Unix seconds → "yyyy-MM-ddTHH:mm:ssZ" (UTC), without DateTime round-trips.</summary>
    private static string ToIso8601Utc(long unixSeconds) =>
        DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", Invariant);
}
