using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Api;

/// <summary>
/// Source-generated serializer context for the Prime API wire format. The API
/// uses PascalCase, so no naming policy is applied and C# property names match
/// the wire names exactly. Source generation is mandatory: the binary ships as
/// Native AOT and cannot use reflection-based serialization.
/// </summary>
[JsonSourceGenerationOptions]
[JsonSerializable(typeof(GetScheduleRequest))]
[JsonSerializable(typeof(Envelope<GetScheduleResult>))]
[JsonSerializable(typeof(GetScheduleRegionsRequest))]
[JsonSerializable(typeof(AccountQueryRequest))]
[JsonSerializable(typeof(GetPlayerSkillRequest))]
[JsonSerializable(typeof(Envelope<GetPlayerSkillResult>))]
[JsonSerializable(typeof(GetPlayerClubDetailsRequest))]
[JsonSerializable(typeof(Envelope<GetPlayerClubDetailsResult>))]
[JsonSerializable(typeof(GetClubStatsRequest))]
[JsonSerializable(typeof(GetPlaylistsRequest))]
[JsonSerializable(typeof(Envelope<GetPlaylistsResult>))]
[JsonSerializable(typeof(GetActivePlaylistsRequest))]
[JsonSerializable(typeof(Envelope<GetActivePlaylistsResult>))]
[JsonSerializable(typeof(Envelope<JsonElement>))]
internal sealed partial class ApiJsonContext : JsonSerializerContext
{
}
