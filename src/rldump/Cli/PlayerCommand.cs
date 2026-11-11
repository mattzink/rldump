using System.CommandLine;

namespace Rldump.Cli;

/// <summary>
/// The "player" command group: look up a player by display name (or Epic
/// account ID). Subcommands: <c>find</c> (account lookup), <c>skills</c>
/// (skill ratings), and <c>club</c> (club details), resolved from the name
/// via the account lookup.
/// </summary>
internal static class PlayerCommand
{
    public static Command Build()
    {
        var player = new Command("player", "Look up a player by name.");
        player.Add(BuildFind());
        player.Add(BuildSkills());
        player.Add(BuildClub());
        return player;
    }

    private static Command BuildFind()
    {
        Option<string> name = NameOption();
        var find = new Command("find", "Resolve a player to their account (ID, provider, linked accounts).");
        find.Add(name);
        find.SetAction(async (parseResult, ct) =>
            await RldumpApp.PlayerFindAsync(parseResult.GetValue(name)!, parseResult.ToCliOptions(), handler: null, ct));
        return find;
    }

    private static Command BuildSkills()
    {
        Option<string> name = NameOption();
        var skills = new Command("skills", "Get a player's skill ratings by name (resolves the account ID first).");
        skills.Add(name);
        skills.SetAction(async (parseResult, ct) =>
            await RldumpApp.PlayerSkillsAsync(parseResult.GetValue(name)!, parseResult.ToCliOptions(), handler: null, ct));
        return skills;
    }

    private static Command BuildClub()
    {
        Option<string> name = NameOption();
        var club = new Command("club", "Get a player's club details.");
        club.Add(name);
        club.SetAction(async (parseResult, ct) =>
            await RldumpApp.PlayerClubAsync(parseResult.GetValue(name)!, parseResult.ToCliOptions(), handler: null, ct));
        return club;
    }

    /// <summary>
    /// A fresh required --name option per subcommand: a System.CommandLine
    /// symbol can only belong to one command, so the two subcommands each get
    /// their own instance.
    /// </summary>
    private static Option<string> NameOption() => new("--name")
    {
        Arity = ArgumentArity.ExactlyOne,
        Required = true,
        Description = "Player display name or Epic account ID (required), e.g. Z1nky.",
    };
}
