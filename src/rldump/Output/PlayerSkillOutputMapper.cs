using Rldump.Api;

namespace Rldump.Output;

/// <summary>
/// Maps the GetPlayerSkill API result to the tidy output model: each skill's
/// raw playlist index keeps its value and gains a title alongside it (from
/// Config/GetPlaylists, "Unknown" when unmapped), and extension data passes
/// through with camelCased keys.
/// </summary>
internal static class PlayerSkillOutputMapper
{
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
            ExtensionData = TidyJson.CamelCaseKeys(skill.ExtensionData),
        };
}
