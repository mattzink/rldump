using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Output;

/// <summary>Top-level tidy output for GetSchedule: { "schedules": [ … ] }.</summary>
internal sealed class ScheduleOutput
{
    public ScheduleEntryOut[] Schedules { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Tidy output for one schedule entry (one start time).</summary>
internal sealed class ScheduleEntryOut
{
    /// <summary>ISO 8601 UTC, e.g. "2026-11-05T01:00:00Z".</summary>
    public string Time { get; init; } = "";

    /// <summary>String, not number: the ID exceeds JS safe-integer precision.</summary>
    public string ScheduleId { get; init; } = "";

    public bool UpdateSkill { get; init; }

    public TourSettingsOut[] Tournaments { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// Tidy output for one tournament. Transformed fields are declared explicitly;
/// every other wire field is carried in <see cref="ExtensionData"/> with its
/// key camelCased by the mapper (the serializer writes extension-data keys
/// verbatim, so the naming policy does not apply to them).
/// </summary>
internal sealed class TourSettingsOut
{
    /// <summary>String, not number: the ID exceeds JS safe-integer precision.</summary>
    public string Id { get; init; } = "";

    /// <summary>String, not number: the ID exceeds JS safe-integer precision.</summary>
    public string ScheduleId { get; init; } = "";

    /// <summary>ISO 8601 UTC.</summary>
    public string StartTime { get; init; } = "";

    /// <summary>ISO 8601 UTC; omitted when absent or null on the wire.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GenerateBracketTime { get; init; }

    /// <summary>ISO 8601 UTC; omitted when absent or null on the wire.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MatchExpireTimestamp { get; init; }

    public bool Public { get; init; }

    /// <summary>Omitted when absent or null on the wire.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsMultiGameMode { get; init; }

    /// <summary>The mode name when known, otherwise the raw index number.</summary>
    public GameModeName GameMode { get; init; }

    /// <summary>Mode names when known, otherwise raw index numbers.</summary>
    public GameModeName[] GameModes { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
