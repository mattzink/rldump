using Rldump.Output;

namespace Rldump.Tests;

public class GameModeMapTests
{
    private static readonly GameModeMap Map = GameModeMap.Load();

    [Fact]
    public void Load_Parses33Entries() => Assert.Equal(33, Map.Modes.Count);

    [Theory]
    [InlineData(0, "Soccar")]
    [InlineData(1, "Hoops")]
    [InlineData(3, "Rumble")]
    [InlineData(5, "Dropshot")]
    [InlineData(12, "Dropshot Rumble")]
    [InlineData(20, "Super Cube")]
    [InlineData(27, "Pentathlon Tournament")]
    [InlineData(33, "Bullet Ball")]
    public void SpotChecks(int index, string name) => Assert.Equal(name, Map.Modes[index]);

    [Fact]
    public void Index4_IsAbsent() => Assert.False(Map.Modes.ContainsKey(4));
}
