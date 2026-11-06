using System.CommandLine;

namespace Rldump.Cli;

/// <summary>
/// The "playlists" command group: <c>all</c> lists every playlist the game
/// knows about (Config/GetPlaylists); <c>active</c> lists the playlists
/// currently live on the servers (Playlists/GetActivePlaylists).
/// </summary>
internal static class PlaylistsCommand
{
    public static Command Build()
    {
        var playlists = new Command("playlists", "Query the game's playlist catalog (all known and currently active playlists).");
        playlists.Add(BuildAll());
        playlists.Add(BuildActive());
        return playlists;
    }

    private static Command BuildAll()
    {
        var all = new Command("all", "List every playlist the game knows about (id, title, player count, flags).");
        all.SetAction(async (parseResult, ct) =>
            await RldumpApp.PlaylistsAllAsync(parseResult.ToCliOptions(), handler: null, ct));
        return all;
    }

    private static Command BuildActive()
    {
        var active = new Command("active", "List the playlists currently active on the live servers, with start/end times.");
        active.SetAction(async (parseResult, ct) =>
            await RldumpApp.PlaylistsActiveAsync(parseResult.ToCliOptions(), handler: null, ct));
        return active;
    }
}
