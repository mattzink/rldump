using System.CommandLine;
using Rldump.Api;

namespace Rldump.Cli;

/// <summary>
/// The "player" command group: look up a player by display name or Epic player
/// ID. Subcommands: <c>find</c> (account lookup), <c>skills</c> (skill ratings),
/// and <c>club</c> (club details).
/// </summary>
internal static class PlayerCommand
{
    public static Command Build()
    {
        var player = new Command("player", "Look up a player by name or Epic ID.");
        player.Add(BuildFind());
        player.Add(BuildSkills());
        player.Add(BuildClub());
        return player;
    }

    private static Command BuildFind()
    {
        var find = new Command("find", "Resolve a player to their account (ID, provider, linked accounts).");
        (Option<string?> name, Option<string?> epicId) = AddPlayerIdOptions(find);
        find.SetAction((parseResult, ct) => RunPlayerAsync(
            parseResult.GetValue(name), parseResult.GetValue(epicId),
            playerId => RldumpApp.PlayerFindAsync(playerId, parseResult.ToCliOptions(), handler: null, ct)));
        return find;
    }

    private static Command BuildSkills()
    {
        var skills = new Command("skills", "Get a player's skill ratings by name or Epic ID.");
        (Option<string?> name, Option<string?> epicId) = AddPlayerIdOptions(skills);
        skills.SetAction((parseResult, ct) => RunPlayerAsync(
            parseResult.GetValue(name), parseResult.GetValue(epicId),
            playerId => RldumpApp.PlayerSkillsAsync(playerId, parseResult.ToCliOptions(), handler: null, ct)));
        return skills;
    }

    private static Command BuildClub()
    {
        var club = new Command("club", "Get a player's club details.");
        (Option<string?> name, Option<string?> epicId) = AddPlayerIdOptions(club);
        club.SetAction((parseResult, ct) => RunPlayerAsync(
            parseResult.GetValue(name), parseResult.GetValue(epicId),
            playerId => RldumpApp.PlayerClubAsync(playerId, parseResult.ToCliOptions(), handler: null, ct)));
        return club;
    }

    private static (Option<string?> Name, Option<string?> EpicId) AddPlayerIdOptions(Command command)
    {
        Option<string?> name = new("--name")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = "Player display name, e.g. Z1nky.",
        };
        Option<string?> epicId = new("--epic-id")
        {
            Arity = ArgumentArity.ExactlyOne,
            Description = "Epic player ID, e.g. Epic|8dd6950d3a2b4a8d964173e775be565b|0.",
        };
        command.Add(name);
        command.Add(epicId);
        return (name, epicId);
    }

    private static async Task<int> RunPlayerAsync(string? name, string? epicId, Func<string, Task<int>> action)
    {
        bool hasName = !string.IsNullOrWhiteSpace(name);
        bool hasEpicId = !string.IsNullOrWhiteSpace(epicId);
        if (hasName == hasEpicId)
        {
            Console.Error.WriteLine("rldump: specify exactly one of --name or --epic-id.");
            return 2;
        }

        if (hasEpicId && !EpicPlayerId.TryGetAccountId(epicId!, out _))
        {
            Console.Error.WriteLine("rldump: --epic-id must use the form Epic|<account ID>|0.");
            return 2;
        }

        return await action(epicId ?? name!);
    }
}
