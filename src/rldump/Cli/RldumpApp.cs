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
}
