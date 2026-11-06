using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Api;

/// <summary>
/// Skills/GetPlayerSkill takes a PlayerID of the form "Epic|&lt;account ID&gt;|0"
/// (the docs' example payload).
/// </summary>
internal sealed class GetPlayerSkillRequest
{
    public string PlayerID { get; init; } = "";
}

/// <summary>
/// Wire model for one skill entry. Only the field that needs more than a
/// pass-through (Playlist, which gets a name alongside the raw index) is
/// declared explicitly; every other wire field (Mu, Sigma, Tier, …) flows
/// through <see cref="ExtensionData"/>.
/// </summary>
internal sealed class SkillEntry
{
    /// <summary>Playlist index; named via Config/GetPlaylists in the tidy output.</summary>
    public int? Playlist { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// The <c>Result</c> payload of Skills/GetPlayerSkill. Only <see cref="Skills"/>
/// is declared explicitly; any other field the API sends (RewardLevels, or new
/// fields) is captured in <see cref="ExtensionData"/> and passed through.
/// </summary>
internal sealed class GetPlayerSkillResult
{
    public List<SkillEntry> Skills { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
