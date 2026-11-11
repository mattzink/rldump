using System.CommandLine;
using System.Globalization;

namespace Rldump.Cli;

internal static class ClubCommand
{
    public static Command Build()
    {
        var club = new Command("club", "Query club data.");
        club.Add(BuildStats());
        return club;
    }

    private static Command BuildStats()
    {
        Option<string> id = new("--id")
        {
            Arity = ArgumentArity.ExactlyOne,
            Required = true,
            Description = "Club ID.",
        };
        var stats = new Command("stats", "Get club career and seasonal stats.");
        stats.Add(id);
        stats.SetAction(async (parseResult, ct) =>
        {
            if (!ulong.TryParse(parseResult.GetValue(id), NumberStyles.None, CultureInfo.InvariantCulture, out ulong clubId) || clubId == 0)
            {
                Console.Error.WriteLine("rldump: --id must be a positive unsigned integer.");
                return 2;
            }

            return await RldumpApp.ClubStatsAsync(clubId, parseResult.ToCliOptions(), handler: null, ct);
        });
        return stats;
    }
}