using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Api;

internal sealed class GetScheduleRequest
{
    public string Region { get; init; } = "";
}

/// <summary>
/// The <c>Result</c> payload of Tournaments/Search/GetSchedule. Only
/// <see cref="Schedules"/> is declared explicitly; any other field the API
/// sends (present or added in the future) is captured in
/// <see cref="ExtensionData"/> and passed through to the tidy output.
/// </summary>
internal sealed class GetScheduleResult
{
    public ScheduleEntry[] Schedules { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>One scheduled tournament block (one entry per start time).</summary>
internal sealed class ScheduleEntry
{
    /// <summary>Unix epoch seconds.</summary>
    public long Time { get; init; }

    public long ScheduleID { get; init; }

    public bool bUpdateSkill { get; init; }

    public TourSettings[] Tournaments { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Tournament settings as returned by GetSchedule. Only the fields the tidy
/// output transforms are declared explicitly; every other wire field (Title,
/// CreatorName, Region, …) is captured in <see cref="ExtensionData"/> and
/// passed through untouched, so new API fields never get dropped.
/// </summary>
internal sealed class TourSettings
{
    public long ID { get; init; }

    public long ScheduleID { get; init; }

    /// <summary>Unix epoch seconds.</summary>
    public long StartTime { get; init; }

    /// <summary>Unix epoch seconds.</summary>
    public long? GenerateBracketTime { get; init; }

    /// <summary>Unix epoch seconds.</summary>
    public long? MatchExpireTimestamp { get; init; }

    public bool bPublic { get; init; }

    public bool? bIsMultiGameMode { get; init; }

    /// <summary>Game-mode index; replaced by its name in the tidy output.</summary>
    public int GameMode { get; init; }

    /// <summary>Game-mode indices for composite modes; replaced by names in the tidy output.</summary>
    public int[] GameModes { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
