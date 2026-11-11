using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Api;

internal sealed class GetPlayerClubDetailsRequest
{
    public string PlayerID { get; init; } = "";
}

internal sealed class GetPlayerClubDetailsResult
{
    public ClubDetails ClubDetails { get; init; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class ClubDetails
{
    public ulong ClubID { get; init; }

    public int? PrimaryColor { get; init; }

    public int? AccentColor { get; init; }

    public ulong? CreatedTime { get; init; }

    public ulong? LastUpdatedTime { get; init; }

    public ulong? NameLastUpdatedTime { get; init; }

    public ulong? DeletedTime { get; init; }

    public bool? bServerReplicated { get; init; }

    public bool? bVerified { get; init; }

    public ClubMember[]? Members { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class ClubMember
{
    public ulong? CreatedTime { get; init; }

    public ulong? DeletedTime { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}