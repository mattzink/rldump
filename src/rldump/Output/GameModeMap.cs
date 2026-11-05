using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rldump.Output;

/// <summary>
/// The game-mode index → name mapping extracted from the game binary
/// (src/rldump/Data/gamemodes.json, embedded as a resource and rebuilt when
/// the game ships a new mapping). Loaded once at startup.
/// </summary>
internal sealed class GameModeMap
{
    public IReadOnlyDictionary<int, string> Modes { get; }

    private GameModeMap(IReadOnlyDictionary<int, string> modes) => Modes = modes;

    public static GameModeMap Load()
    {
        Assembly assembly = typeof(GameModeMap).Assembly;
        string resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("gamemodes.json", StringComparison.Ordinal));

        byte[] json;
        using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
        using (MemoryStream buffer = new())
        {
            stream.CopyTo(buffer);
            json = buffer.ToArray();
        }

        GameModeEntry[] entries = JsonSerializer.Deserialize(json, GameModeJsonContext.Default.GameModeEntryArray)
            ?? throw new InvalidOperationException("embedded gamemodes.json deserialized to null");

        Dictionary<int, string> modes = new(entries.Length);
        foreach (GameModeEntry entry in entries)
            modes[entry.Index] = entry.Name;
        return new GameModeMap(modes);
    }
}

/// <summary>One entry of the embedded gamemodes.json; only Index and Name are used.</summary>
internal sealed class GameModeEntry
{
    [JsonPropertyName("GameMode Index")]
    public int Index { get; init; }

    [JsonPropertyName("GameMode Name")]
    public string Name { get; init; } = "";
}

[JsonSerializable(typeof(GameModeEntry[]))]
internal sealed partial class GameModeJsonContext : JsonSerializerContext
{
}
