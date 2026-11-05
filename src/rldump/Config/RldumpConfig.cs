using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Config;

/// <summary>The resolved configuration after the search-order merge.</summary>
internal sealed class RldumpConfig
{
    public const string DefaultBaseUrl = "https://prime.rocketplanet.gg";

    public string Token { get; }
    public string BaseUrl { get; }

    public RldumpConfig(string token, string? baseUrl)
    {
        Token = token;
        BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
    }
}

/// <summary>
/// The shape of rldump.json: { "api": { "baseUrl": …, "token": … } }.
/// baseUrl is optional (defaults to the Prime host); token is required.
/// </summary>
internal sealed class ConfigFile
{
    public ConfigApi? Api { get; init; }
}

internal sealed class ConfigApi
{
    public string? BaseUrl { get; init; }
    public string? Token { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ConfigFile))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext
{
}

/// <summary>
/// A configuration problem: no config found, missing token, malformed JSON,
/// unreadable file. Maps to exit code 3.
/// </summary>
internal sealed class ConfigException : Exception
{
    public ConfigException(string message) : base(message)
    {
    }
}
