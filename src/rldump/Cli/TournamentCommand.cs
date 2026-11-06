using System.CommandLine;

namespace Rldump.Cli;

/// <summary>
/// The "tournament" command group: wraps <c>schedule</c> and <c>regions</c> so
/// the command tree mirrors the API's multi-level service layout
/// (Tournaments/…, Config/…).
/// </summary>
internal static class TournamentCommand
{
    public static Command Build()
    {
        var tournament = new Command("tournament", "Query the tournament schedule and its regions.");
        tournament.Add(ScheduleCommand.Build());
        tournament.Add(RegionsCommand.Build());
        return tournament;
    }
}
