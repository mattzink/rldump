using System.Text.Json;

namespace Rldump.Config;

/// <summary>
/// Locates and loads rldump.json. Search order: an explicit --config path
/// (exclusive), then ./rldump.json in the current directory, then
/// $XDG_CONFIG_HOME/rldump.json (falling back to ~/.config/rldump.json
/// when XDG_CONFIG_HOME is unset or empty). When both default files exist
/// they merge per field, with the cwd file winning.
/// </summary>
internal static class ConfigLoader
{
    public const string ConfigFileName = "rldump.json";

    public static RldumpConfig Load(string? explicitPath, string cwd, string? xdgConfigHome, string? home)
    {
        if (!string.IsNullOrEmpty(explicitPath))
        {
            string path = Path.GetFullPath(explicitPath, cwd);
            if (!File.Exists(path))
                throw new ConfigException($"no config file at '{path}'");
            return RequireToken(ReadConfigFile(path), path);
        }

        string cwdPath = Path.Combine(cwd, ConfigFileName);
        string? xdgPath = XdgConfigPath(xdgConfigHome, home);

        List<string> candidates = [cwdPath];
        if (xdgPath is not null)
            candidates.Add(xdgPath);

        string? token = null;
        string? baseUrl = null;
        List<string> searched = [];

        foreach (string path in candidates)
        {
            if (!File.Exists(path))
                continue;
            searched.Add(path);
            ConfigFile file = ReadConfigFile(path);
            // First non-null wins: the cwd file is searched before the XDG file.
            token ??= file.Api?.Token;
            baseUrl ??= file.Api?.BaseUrl;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            if (searched.Count == 0)
                throw new ConfigException(
                    $"no API token: no {ConfigFileName} in '{cwd}' and no {xdgPath ?? "~/.config/rldump.json"}");
            throw new ConfigException(
                $"no API token: config file(s) {string.Join(" and ", searched)} do not contain a non-empty api.token");
        }

        return new RldumpConfig(token.Trim(), baseUrl);
    }

    private static string? XdgConfigPath(string? xdgConfigHome, string? home)
    {
        if (!string.IsNullOrEmpty(xdgConfigHome))
            return Path.Combine(xdgConfigHome, ConfigFileName);
        if (!string.IsNullOrEmpty(home))
            return Path.Combine(home, ".config", ConfigFileName);
        return null;
    }

    private static ConfigFile ReadConfigFile(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new ConfigException($"cannot read config file '{path}': {ex.Message}");
        }

        ConfigFile? file;
        try
        {
            file = JsonSerializer.Deserialize(text, ConfigJsonContext.Default.ConfigFile);
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"invalid config file '{path}': {ex.Message}");
        }

        return file ?? throw new ConfigException($"invalid config file '{path}': file is empty or not a JSON object");
    }

    private static RldumpConfig RequireToken(ConfigFile file, string path)
    {
        string? token = file.Api?.Token;
        if (string.IsNullOrWhiteSpace(token))
            throw new ConfigException($"no API token: config file '{path}' does not contain a non-empty api.token");
        return new RldumpConfig(token.Trim(), file.Api?.BaseUrl);
    }
}
