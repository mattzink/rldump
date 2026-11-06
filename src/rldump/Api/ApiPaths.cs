namespace Rldump.Api;

/// <summary>Prime API route paths (the docs SPA at /docs lists them all).</summary>
internal static class ApiPaths
{
    public const string GetSchedule = "/Tournaments/Search/GetSchedule";
    public const string GetScheduleRegions = "/Config/GetScheduleRegions";
    public const string AccountQuery = "/GraphQL/AccountQuery";
    public const string GetPlayerSkill = "/Skills/GetPlayerSkill";
    public const string GetPlaylists = "/Config/GetPlaylists";
    public const string GetActivePlaylists = "/Playlists/GetActivePlaylists";
}
