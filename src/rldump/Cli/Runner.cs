using Rldump.Api;
using Rldump.Config;

namespace Rldump.Cli;

/// <summary>
/// Shared invocation pipeline for every command: load the config, build the
/// client, run the action, write the JSON output, and map failures to the
/// documented exit codes:
///   0 success · 1 API error (envelope or non-2xx) · 3 config or output-file
///   error · 4 network/timeout.
/// JSON goes to stdout (or -o FILE); diagnostics go to stderr. Never mixed.
/// </summary>
internal static class Runner
{
    public static async Task<int> RunAsync(
        Func<RocketPlanetClient, CancellationToken, Task<string>> action,
        CliOptions options,
        HttpMessageHandler? handler,
        CancellationToken ct)
    {
        try
        {
            RldumpConfig config = ConfigLoader.Load(
                options.ConfigPath,
                Environment.CurrentDirectory,
                Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),
                Environment.GetEnvironmentVariable("HOME"));

            RocketPlanetClient client = new(
                config.BaseUrl, config.Token, TimeSpan.FromSeconds(options.TimeoutSeconds), handler);

            string json = await action(client, ct);
            return WriteOutput(json, options.OutPath);
        }
        catch (ConfigException ex)
        {
            Console.Error.WriteLine($"rldump: {ex.Message}");
            return 3;
        }
        catch (ApiErrorException ex)
        {
            Console.Error.WriteLine($"rldump: API error ({ex.Type}): {ex.Message}");
            return 1;
        }
        catch (ApiHttpException ex)
        {
            Console.Error.WriteLine($"rldump: {ex.Message}");
            return 1;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            Console.Error.WriteLine($"rldump: request timed out after {options.TimeoutSeconds}s");
            return 4;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"rldump: request failed: {ex.Message}");
            return 4;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"rldump: cannot write output: {ex.Message}");
            return 3;
        }
    }

    private static int WriteOutput(string json, string? outPath)
    {
        string text = json + "\n";
        if (outPath is null)
            Console.Out.Write(text);
        else
            File.WriteAllText(outPath, text);
        return 0;
    }
}
