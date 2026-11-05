namespace Rldump.Tests;

/// <summary>Reads test fixtures copied to the test output directory.</summary>
internal static class Fixtures
{
    public static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
}
