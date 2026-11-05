using System.CommandLine;

namespace Rldump.Cli;

/// <summary>The resolved global options for one command invocation.</summary>
internal sealed record CliOptions(string? ConfigPath, bool Raw, bool Compact, string? OutPath, int TimeoutSeconds);

internal static class ParseResultExtensions
{
    public static CliOptions ToCliOptions(this ParseResult parseResult) => new(
        parseResult.GetValue<string?>(GlobalOptions.Config),
        parseResult.GetValue<bool>(GlobalOptions.Raw),
        parseResult.GetValue<bool>(GlobalOptions.Compact),
        parseResult.GetValue<string?>(GlobalOptions.Out),
        parseResult.GetValue<int>(GlobalOptions.Timeout));
}
