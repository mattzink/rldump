using System.Globalization;
using Rldump.Api;

namespace Rldump.Output;

internal static class PlayerClubOutputMapper
{
    public static PlayerClubOutput Map(GetPlayerClubDetailsResult result)
    {
        ClubDetails club = result.ClubDetails;
        return new PlayerClubOutput
        {
            ClubDetails = new ClubDetailsOutput
            {
                ClubID = club.ClubID.ToString(CultureInfo.InvariantCulture),
                PrimaryColor = club.PrimaryColor,
                PrimaryHexColor = ToCssHexColor(club.PrimaryColor),
                AccentColor = club.AccentColor,
                AccentHexColor = ToCssHexColor(club.AccentColor),
                CreatedTime = ToIso8601Utc(club.CreatedTime),
                LastUpdatedTime = ToIso8601Utc(club.LastUpdatedTime),
                NameLastUpdatedTime = ToIso8601Utc(club.NameLastUpdatedTime),
                DeletedTime = ToIso8601Utc(club.DeletedTime),
                ServerReplicated = club.bServerReplicated,
                Verified = club.bVerified,
                Members = club.Members?.Select(MapMember).ToArray(),
                ExtensionData = TidyJson.CamelCaseKeys(club.ExtensionData),
            },
            ExtensionData = TidyJson.CamelCaseKeys(result.ExtensionData),
        };
    }

    private static ClubMemberOutput MapMember(ClubMember member) => new()
    {
        CreatedTime = ToIso8601Utc(member.CreatedTime),
        DeletedTime = ToIso8601Utc(member.DeletedTime),
        ExtensionData = TidyJson.CamelCaseKeys(member.ExtensionData),
    };

    private static string? ToCssHexColor(int? packedColor)
    {
        if (packedColor is null)
            return null;

        uint argb = unchecked((uint)packedColor.Value);
        string rgb = (argb & 0x00FFFFFF).ToString("X6", CultureInfo.InvariantCulture);
        byte alpha = (byte)(argb >> 24);
        return alpha == byte.MaxValue
            ? $"#{rgb}"
            : $"#{rgb}{alpha.ToString("X2", CultureInfo.InvariantCulture)}";
    }

    private static string? ToIso8601Utc(ulong? unixSeconds) =>
        unixSeconds is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(checked((long)unixSeconds.Value))
                .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : null;
}