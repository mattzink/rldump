using System.Text.Json;
using Rldump.Api;
using Rldump.Output;

namespace Rldump.Cli;

/// <summary>
/// The core command implementations, decoupled from System.CommandLine so
/// tests can drive them directly with a fake HTTP handler. Each method returns
/// the process exit code and writes the JSON output (stdout or -o FILE).
/// </summary>
internal static class RldumpApp
{
    private static readonly Lazy<GameModeMap> ModeMap = new(GameModeMap.Load);

    public static async Task<int> ScheduleAsync(string region, CliOptions options, HttpMessageHandler? handler, CancellationToken ct)
    {
        return await Runner.RunAsync(async (client, token) =>
        {
            GetScheduleRequest request = new() { Region = region };

            if (options.Raw)
            {
                string body = await client.QueryRawAsync(ApiPaths.GetSchedule, request, ApiJsonContext.Default.GetScheduleRequest, token);
                return JsonFormat.Format(body, options.Compact);
            }

            GetScheduleResult result = await client.QueryAsync(
                ApiPaths.GetSchedule, request,
                ApiJsonContext.Default.GetScheduleRequest, ApiJsonContext.Default.EnvelopeGetScheduleResult, token);
            ScheduleOutput output = ScheduleOutputMapper.Map(result, ModeMap.Value.Modes);
            return JsonFormat.Format(JsonSerialize.Schedule(output), options.Compact);
        }, options, handler, ct);
    }

    public static async Task<int> RegionsAsync(CliOptions options, HttpMessageHandler? handler, CancellationToken ct)
    {
        return await Runner.RunAsync(async (client, token) =>
        {
            GetScheduleRegionsRequest request = new();

            if (options.Raw)
            {
                string body = await client.QueryRawAsync(ApiPaths.GetScheduleRegions, request, ApiJsonContext.Default.GetScheduleRegionsRequest, token);
                return JsonFormat.Format(body, options.Compact);
            }

            // The contract marks the result opaque, so the Result payload is
            // printed verbatim: no transformation is possible for an unknown
            // shape.
            JsonElement result = await client.QueryOpaqueAsync(
                ApiPaths.GetScheduleRegions, request,
                ApiJsonContext.Default.GetScheduleRegionsRequest, ApiJsonContext.Default.EnvelopeJsonElement, token);
            return JsonFormat.Format(result.GetRawText(), options.Compact);
        }, options, handler, ct);
    }

    /// <summary>
    /// player find: GraphQL/AccountQuery by display name (or Epic account ID).
    /// No field of the result needs a value transform, so tidy mode passes the
    /// Result payload through with recursively camelCased keys; raw mode prints
    /// the full envelope verbatim.
    /// </summary>
    public static async Task<int> PlayerFindAsync(string name, CliOptions options, HttpMessageHandler? handler, CancellationToken ct)
    {
        return await Runner.RunAsync(async (client, token) =>
        {
            AccountQueryRequest request = new() { User = name };

            if (options.Raw)
            {
                string body = await client.QueryRawAsync(ApiPaths.AccountQuery, request, ApiJsonContext.Default.AccountQueryRequest, token);
                return JsonFormat.Format(body, options.Compact);
            }

            JsonElement result = await client.QueryOpaqueAsync(
                ApiPaths.AccountQuery, request,
                ApiJsonContext.Default.AccountQueryRequest, ApiJsonContext.Default.EnvelopeJsonElement, token);
            return JsonFormat.Format(TidyJson.CamelCaseKeys(result), options.Compact);
        }, options, handler, ct);
    }

    /// <summary>
    /// player skills: resolve the name to an account ID via GraphQL/AccountQuery,
    /// then call Skills/GetPlayerSkill with PlayerID "Epic|&lt;account ID&gt;|0"
    /// (the docs' example payload). Tidy mode keeps each skill's raw playlist
    /// index and adds its title alongside (fetched live from Config/GetPlaylists;
    /// "Unknown" when a playlist has no title). Raw mode prints the
    /// GetPlayerSkill envelope verbatim (the account lookup is an internal step
    /// and the playlist lookup is skipped).
    /// </summary>
    public static async Task<int> PlayerSkillsAsync(string name, CliOptions options, HttpMessageHandler? handler, CancellationToken ct)
    {
        return await Runner.RunAsync(async (client, token) =>
        {
            AccountQueryRequest findRequest = new() { User = name };
            JsonElement account = await client.QueryOpaqueAsync(
                ApiPaths.AccountQuery, findRequest,
                ApiJsonContext.Default.AccountQueryRequest, ApiJsonContext.Default.EnvelopeJsonElement, token);
            string accountId = account.GetProperty("AccountID").GetString()
                ?? throw new ApiErrorException("MalformedResponse", "the account lookup did not return an AccountID");

            GetPlayerSkillRequest request = new() { PlayerID = $"Epic|{accountId}|0" };

            if (options.Raw)
            {
                string body = await client.QueryRawAsync(ApiPaths.GetPlayerSkill, request, ApiJsonContext.Default.GetPlayerSkillRequest, token);
                return JsonFormat.Format(body, options.Compact);
            }

            GetPlayerSkillResult result = await client.QueryAsync(
                ApiPaths.GetPlayerSkill, request,
                ApiJsonContext.Default.GetPlayerSkillRequest, ApiJsonContext.Default.EnvelopeGetPlayerSkillResult, token);

            // Playlist names come from the live API (not embedded data), so new
            // game playlists need no rebuild.
            GetPlaylistsResult playlists = await client.QueryAsync(
                ApiPaths.GetPlaylists, new GetPlaylistsRequest(),
                ApiJsonContext.Default.GetPlaylistsRequest, ApiJsonContext.Default.EnvelopeGetPlaylistsResult, token);

            PlayerSkillOutput output = PlayerSkillOutputMapper.Map(result, PlaylistNames(playlists));
            return JsonFormat.Format(JsonSerialize.Skills(output), options.Compact);
        }, options, handler, ct);
    }

    /// <summary>
    /// playlists all: Config/GetPlaylists — the full playlist catalog the game
    /// knows about. No field of the Result needs a value transform (small
    /// integer IDs, no timestamps or b-prefixed fields), so tidy mode passes
    /// the Result payload through with recursively camelCased keys; raw mode
    /// prints the full envelope verbatim.
    /// </summary>
    public static async Task<int> PlaylistsAllAsync(CliOptions options, HttpMessageHandler? handler, CancellationToken ct)
    {
        return await Runner.RunAsync(async (client, token) =>
        {
            GetPlaylistsRequest request = new();

            if (options.Raw)
            {
                string body = await client.QueryRawAsync(ApiPaths.GetPlaylists, request, ApiJsonContext.Default.GetPlaylistsRequest, token);
                return JsonFormat.Format(body, options.Compact);
            }

            JsonElement result = await client.QueryOpaqueAsync(
                ApiPaths.GetPlaylists, request,
                ApiJsonContext.Default.GetPlaylistsRequest, ApiJsonContext.Default.EnvelopeJsonElement, token);
            return JsonFormat.Format(TidyJson.CamelCaseKeys(result), options.Compact);
        }, options, handler, ct);
    }

    /// <summary>
    /// playlists active: Playlists/GetActivePlaylists — the playlists currently
    /// live on the servers. Tidy mode keeps each entry's raw playlist index and
    /// adds its title alongside (fetched live from Config/GetPlaylists;
    /// "Unknown" when unmapped), turns start/end times into ISO 8601 UTC, and
    /// bCompetitive into competitive. Raw mode prints the GetActivePlaylists
    /// envelope verbatim (the playlist lookup is skipped).
    /// </summary>
    public static async Task<int> PlaylistsActiveAsync(CliOptions options, HttpMessageHandler? handler, CancellationToken ct)
    {
        return await Runner.RunAsync(async (client, token) =>
        {
            GetActivePlaylistsRequest request = new();

            if (options.Raw)
            {
                string body = await client.QueryRawAsync(ApiPaths.GetActivePlaylists, request, ApiJsonContext.Default.GetActivePlaylistsRequest, token);
                return JsonFormat.Format(body, options.Compact);
            }

            GetActivePlaylistsResult result = await client.QueryAsync(
                ApiPaths.GetActivePlaylists, request,
                ApiJsonContext.Default.GetActivePlaylistsRequest, ApiJsonContext.Default.EnvelopeGetActivePlaylistsResult, token);

            // Playlist names come from the live API (not embedded data), so new
            // game playlists need no rebuild.
            GetPlaylistsResult playlists = await client.QueryAsync(
                ApiPaths.GetPlaylists, new GetPlaylistsRequest(),
                ApiJsonContext.Default.GetPlaylistsRequest, ApiJsonContext.Default.EnvelopeGetPlaylistsResult, token);

            ActivePlaylistsOutput output = ActivePlaylistsOutputMapper.Map(result, PlaylistNames(playlists));
            return JsonFormat.Format(JsonSerialize.ActivePlaylists(output), options.Compact);
        }, options, handler, ct);
    }

    /// <summary>Playlist ID → title; entries without a title are dropped so they map to "Unknown".</summary>
    private static Dictionary<int, string> PlaylistNames(GetPlaylistsResult playlists)
    {
        Dictionary<int, string> names = new(playlists.Playlists.Count);
        foreach (PlaylistEntry playlist in playlists.Playlists)
            if (!string.IsNullOrEmpty(playlist.Title))
                names.TryAdd(playlist.ID, playlist.Title);
        return names;
    }
}
