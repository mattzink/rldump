using System.CommandLine;

namespace Rldump.Cli;

/// <summary>
/// Global options shared by every subcommand. Attached to the root command;
/// System.CommandLine 2.0 makes parent options available to subcommands.
/// </summary>
internal static class GlobalOptions
{
    public static readonly Option<string?> Config = new("--config")
    {
        Description = "Use a specific config file (skips the default search).",
    };

    public static readonly Option<bool> Raw = new("--raw")
    {
        Description = "Print the API's JSON response unmodified (default: tidy output).",
    };

    public static readonly Option<bool> Compact = new("--compact")
    {
        Description = "Print compact JSON (default: indented).",
    };

    public static readonly Option<string?> Out = new("-o", ["--out"])
    {
        Description = "Write JSON to FILE instead of stdout.",
    };

    public static readonly Option<int> Timeout = new("--timeout")
    {
        DefaultValueFactory = _ => 30,
        Description = "HTTP timeout in seconds (default 30).",
    };

    public static void AddAll(Command command)
    {
        command.Add(Config);
        command.Add(Raw);
        command.Add(Compact);
        command.Add(Out);
        command.Add(Timeout);
    }
}
