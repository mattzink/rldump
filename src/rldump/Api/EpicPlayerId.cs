namespace Rldump.Api;

internal static class EpicPlayerId
{
    public static bool TryGetAccountId(string playerId, out string accountId)
    {
        const string prefix = "Epic|";
        const string suffix = "|0";
        accountId = "";

        if (!playerId.StartsWith(prefix, StringComparison.Ordinal) ||
            !playerId.EndsWith(suffix, StringComparison.Ordinal) ||
            playerId.Length <= prefix.Length + suffix.Length)
            return false;

        accountId = playerId[prefix.Length..^suffix.Length];
        return !accountId.Contains('|');
    }
}