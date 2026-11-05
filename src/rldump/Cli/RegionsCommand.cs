using System.CommandLine;

namespace Rldump.Cli;

internal static class RegionsCommand
{
    public static Command Build()
    {
        var command = new Command("regions", "List the valid schedule regions.");
        command.SetAction(async (parseResult, ct) =>
            await RldumpApp.RegionsAsync(parseResult.ToCliOptions(), handler: null, ct));
        return command;
    }
}
