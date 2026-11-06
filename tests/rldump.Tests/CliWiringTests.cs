using Rldump.Cli;

namespace Rldump.Tests;

/// <summary>
/// System.CommandLine wiring tests: option parsing, required options, and the
/// usage-error exit code (2) through the same entry point as Main.
/// </summary>
public class CliWiringTests
{
    [Fact]
    public async Task ScheduleWithoutRegion_Exit2_UsageOnStderr()
    {
        using var console = new ConsoleCapture();

        int code = await Program.Run(["tournament", "schedule"]);

        Assert.Equal(2, code);
        Assert.Contains("--region", console.StdErr.ToString());
    }

    [Fact]
    public async Task PlayerFindWithoutName_Exit2_UsageOnStderr()
    {
        using var console = new ConsoleCapture();

        int code = await Program.Run(["player", "find"]);

        Assert.Equal(2, code);
        Assert.Contains("--name", console.StdErr.ToString());
    }

    [Fact]
    public async Task BareGroupCommand_Exit2_UsageOnStdout()
    {
        using var console = new ConsoleCapture();

        int code = await Program.Run(["tournament"]);

        // A group with no subcommand is a usage error (System.CommandLine's
        // "Required command was not provided."), not a success. The error goes
        // to stderr; the usage block (with the subcommands) goes to stdout.
        Assert.Equal(2, code);
        Assert.Contains("Required command was not provided.", console.StdErr.ToString());
        Assert.Contains("schedule", console.StdOut.ToString());
        Assert.Contains("regions", console.StdOut.ToString());
    }

    [Fact]
    public async Task BarePlaylistsGroup_Exit2_UsageOnStdout()
    {
        using var console = new ConsoleCapture();

        int code = await Program.Run(["playlists"]);

        Assert.Equal(2, code);
        Assert.Contains("Required command was not provided.", console.StdErr.ToString());
        // The usage block lists the group's subcommands.
        string stdout = console.StdOut.ToString();
        Assert.Matches("(?m)^\\s+all\\s", stdout);
        Assert.Matches("(?m)^\\s+active\\s", stdout);
    }

    [Fact]
    public async Task UnknownCommand_Exit2()
    {
        using var console = new ConsoleCapture();

        int code = await Program.Run(["bogus"]);

        Assert.Equal(2, code);
    }

    [Fact]
    public async Task UnknownSubcommand_Exit2()
    {
        using var console = new ConsoleCapture();

        int code = await Program.Run(["tournament", "bogus"]);

        Assert.Equal(2, code);
    }

    [Fact]
    public async Task UnknownOption_Exit2()
    {
        using var console = new ConsoleCapture();

        int code = await Program.Run(["tournament", "schedule", "--region", "USW", "--bogus"]);

        Assert.Equal(2, code);
    }

    [Fact]
    public async Task Help_Exit0_GroupsListedOnStdout()
    {
        using var console = new ConsoleCapture();

        int code = await Program.Run(["--help"]);

        Assert.Equal(0, code);
        string stdout = console.StdOut.ToString();
        // The root help lists the command groups; the nested commands show up
        // in the group help.
        Assert.Contains("tournament", stdout);
        Assert.Contains("player", stdout);
        Assert.Contains("playlists", stdout);
        Assert.Contains("--raw", stdout);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(stdout, "(?m)^\\s+--version\\b"));
    }

    [Fact]
    public async Task Version_Exit0_PrintsVersion()
    {
        using var console = new ConsoleCapture();

        int code = await Program.Run(["--version"]);

        Assert.Equal(0, code);
        // System.CommandLine's default --version prints the *entry* assembly's
        // informational version. In the test host the entry assembly is the test
        // assembly (no <Version> in the test csproj — the SDK default), not
        // rldump's, so assert the shape, not the value. The optional +metadata
        // suffix: on CI the SDK appends +<GITHUB_SHA> (SourceRevisionId) to the
        // test assembly's informational version — it is only suppressed in the
        // main csproj. The real binary's version is checked by the CI smoke step.
        Assert.Matches(@"^\d+\.\d+\.\d+(\.\d+)?(\+\S+)?\n$", console.StdOut.ToString());
    }
}
