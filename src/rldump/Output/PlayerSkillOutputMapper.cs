using Rldump.Api;
using System;

namespace Rldump.Output;

/// <summary>
/// Maps the GetPlayerSkill API result to the tidy output model: each skill's
/// raw playlist index keeps its value and gains a title alongside it (from
/// Config/GetPlaylists, "Unknown" when unmapped), and extension data passes
/// through with camelCased keys.
/// </summary>
internal static class PlayerSkillOutputMapper
{
    private static readonly string[] TierNames =
    [
        "Unranked",
        "Bronze I", "Bronze II", "Bronze III",
        "Silver I", "Silver II", "Silver III",
        "Gold I", "Gold II", "Gold III",
        "Platinum I", "Platinum II", "Platinum III",
        "Diamond I", "Diamond II", "Diamond III",
        "Champion I", "Champion II", "Champion III",
        "Grand Champion I", "Grand Champion II", "Grand Champion III",
        "Supersonic Legend",
    ];

    public static PlayerSkillOutput Map(GetPlayerSkillResult result, IReadOnlyDictionary<int, string> playlistNames)
    {
        return new PlayerSkillOutput
        {
            Skills = result.Skills.Select(skill => MapSkill(skill, playlistNames)).ToArray(),
            ExtensionData = TidyJson.CamelCaseKeys(result.ExtensionData),
        };
    }

    private static SkillOut MapSkill(SkillEntry skill, IReadOnlyDictionary<int, string> playlistNames) =>
        new()
        {
            Playlist = skill.Playlist,
            PlaylistName = skill.Playlist is int id && playlistNames.TryGetValue(id, out string? name)
                ? name
                : "Unknown",
            Mu = skill.Mu,
            Rating = skill.Mu is double mu
                ? (int)Math.Round(100 + 20 * mu, MidpointRounding.AwayFromZero)
                : null,
            Tier = skill.Tier,
            TierName = skill.Tier is int tier ? GetTierName(tier) : null,
            ExtensionData = TidyJson.CamelCaseKeys(skill.ExtensionData),
        };

    private static string GetTierName(int tier) =>
        tier >= 0 && tier < TierNames.Length ? TierNames[tier] : "Unknown";
}
