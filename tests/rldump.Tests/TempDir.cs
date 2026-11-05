namespace Rldump.Tests;

/// <summary>A temp directory (with unique name) that is removed when disposed.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("rldump-test-").FullName;

    /// <summary>Writes <paramref name="json"/> at the given path relative to <see cref="Path"/>.</summary>
    public string WriteConfig(string relativePath, string json)
    {
        string fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, json);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }
}
