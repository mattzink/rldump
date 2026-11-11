using System.CommandLine;

namespace Rldump.Cli;

/// <summary>Builds the root command with the global options and subcommands.</summary>
internal static class AppRoot
{
    public static RootCommand Build()
    {
        var root = new RootCommand("Query the Rocket League Prime API and print the result as JSON.");
        GlobalOptions.AddAll(root);
        root.Add(TournamentCommand.Build());
        root.Add(PlayerCommand.Build());
        root.Add(ClubCommand.Build());
        root.Add(PlaylistsCommand.Build());
        // The root command provides -?/-h/--help and --version automatically.
        return root;
    }
}
