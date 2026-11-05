using System.CommandLine;
using Rldump.Cli;

internal static class Program
{
    private static Task<int> Main(string[] args) => Run(args);

    /// <summary>Parses and invokes the CLI; maps failures to the documented exit codes.</summary>
    internal static async Task<int> Run(string[] args)
    {
        RootCommand root = AppRoot.Build();
        ParseResult parseResult = root.Parse(args);
        bool hadParseErrors = parseResult.Errors.Count > 0;

        try
        {
            int code = await parseResult.InvokeAsync();
            // System.CommandLine exits 1 on parse errors by default; the rldump
            // contract reserves exit 2 for usage errors (API errors are 1).
            return hadParseErrors ? 2 : code;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"rldump: unexpected error: {ex.Message}");
            return 1;
        }
    }
}
