using System.CommandLine;

namespace Rldump.Cli;

internal static class ScheduleCommand
{
    public static readonly Option<string> Region = new("--region")
    {
        Arity = ArgumentArity.ExactlyOne,
        Description = "Schedule region (required), e.g. USW.",
    };

    public static Command Build()
    {
        Region.Required = true;

        var command = new Command("schedule", "Get the current tournament schedule for a region.");
        command.Add(Region);
        command.SetAction(async (parseResult, ct) =>
        {
            string region = parseResult.GetValue(Region)!;
            return await RldumpApp.ScheduleAsync(region, parseResult.ToCliOptions(), handler: null, ct);
        });
        return command;
    }
}
