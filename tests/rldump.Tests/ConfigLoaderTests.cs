using Rldump.Config;

namespace Rldump.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void Load_CwdConfigOnly_ReturnsTokenWithDefaultBaseUrl()
    {
        using var cwd = new TempDir();
        using var xdg = new TempDir();
        cwd.WriteConfig("rldump.json", """{ "api": { "token": "cwd-token" } }""");

        RldumpConfig config = ConfigLoader.Load(null, cwd.Path, xdg.Path, null);

        Assert.Equal("cwd-token", config.Token);
        Assert.Equal(RldumpConfig.DefaultBaseUrl, config.BaseUrl);
    }

    [Fact]
    public void Load_XdgConfigOnly_ReturnsTokenAndTrimmedBaseUrl()
    {
        using var cwd = new TempDir();
        using var xdg = new TempDir();
        xdg.WriteConfig("rldump.json", """{ "api": { "token": "xdg-token", "baseUrl": "https://example.test/" } }""");

        RldumpConfig config = ConfigLoader.Load(null, cwd.Path, xdg.Path, null);

        Assert.Equal("xdg-token", config.Token);
        Assert.Equal("https://example.test", config.BaseUrl);
    }

    [Fact]
    public void Load_BothFiles_MergePerField_CwdWins()
    {
        using var cwd = new TempDir();
        using var xdg = new TempDir();
        cwd.WriteConfig("rldump.json", """{ "api": { "token": "cwd-token" } }""");
        xdg.WriteConfig("rldump.json", """{ "api": { "token": "xdg-token", "baseUrl": "https://example.test" } }""");

        RldumpConfig config = ConfigLoader.Load(null, cwd.Path, xdg.Path, null);

        Assert.Equal("cwd-token", config.Token);
        Assert.Equal("https://example.test", config.BaseUrl);
    }

    [Fact]
    public void Load_HomeFallback_UsedWhenXdgConfigHomeUnset()
    {
        using var cwd = new TempDir();
        using var home = new TempDir();
        home.WriteConfig(".config/rldump.json", """{ "api": { "token": "home-token" } }""");

        RldumpConfig config = ConfigLoader.Load(null, cwd.Path, null, home.Path);

        Assert.Equal("home-token", config.Token);
    }

    [Fact]
    public void Load_ExplicitPath_UsesOnlyThatFile()
    {
        using var cwd = new TempDir();
        using var xdg = new TempDir();
        using var other = new TempDir();
        string explicitPath = other.WriteConfig("rldump.json", """{ "api": { "token": "explicit-token" } }""");
        cwd.WriteConfig("rldump.json", """{ "api": { "token": "cwd-token" } }""");
        xdg.WriteConfig("rldump.json", """{ "api": { "token": "xdg-token" } }""");

        RldumpConfig config = ConfigLoader.Load(explicitPath, cwd.Path, xdg.Path, null);

        Assert.Equal("explicit-token", config.Token);
    }

    [Fact]
    public void Load_ExplicitPathMissing_Throws()
    {
        using var cwd = new TempDir();

        ConfigException ex = Assert.Throws<ConfigException>(() =>
            ConfigLoader.Load("/nonexistent/rldump.json", cwd.Path, null, null));

        Assert.Contains("/nonexistent/rldump.json", ex.Message);
    }

    [Fact]
    public void Load_NoFiles_Throws_NamingBothSearchPaths()
    {
        using var cwd = new TempDir();
        using var xdg = new TempDir();

        ConfigException ex = Assert.Throws<ConfigException>(() =>
            ConfigLoader.Load(null, cwd.Path, xdg.Path, null));

        Assert.Contains("no API token", ex.Message);
        Assert.Contains($"no rldump.json in '{cwd.Path}'", ex.Message);
        Assert.Contains(Path.Combine(xdg.Path, "rldump.json"), ex.Message);
    }

    [Fact]
    public void Load_FileWithoutToken_Throws()
    {
        using var cwd = new TempDir();
        using var xdg = new TempDir();
        cwd.WriteConfig("rldump.json", """{ "api": { "baseUrl": "https://example.test" } }""");

        ConfigException ex = Assert.Throws<ConfigException>(() =>
            ConfigLoader.Load(null, cwd.Path, xdg.Path, null));

        Assert.Contains("no API token", ex.Message);
    }

    [Theory]
    [InlineData("""{ "api": { "token": "   " } }""")]
    [InlineData("""{ "api": {} }""")]
    [InlineData("""{ }""")]
    public void Load_MissingOrEmptyToken_Throws(string json)
    {
        using var cwd = new TempDir();
        using var xdg = new TempDir();
        cwd.WriteConfig("rldump.json", json);

        Assert.Throws<ConfigException>(() => ConfigLoader.Load(null, cwd.Path, xdg.Path, null));
    }

    [Fact]
    public void Load_MalformedJson_Throws_WithPath()
    {
        using var cwd = new TempDir();
        using var xdg = new TempDir();
        cwd.WriteConfig("rldump.json", "{ not json");

        ConfigException ex = Assert.Throws<ConfigException>(() =>
            ConfigLoader.Load(null, cwd.Path, xdg.Path, null));

        Assert.Contains(Path.Combine(cwd.Path, "rldump.json"), ex.Message);
    }

    [Fact]
    public void Load_WrongTokenType_Throws()
    {
        using var cwd = new TempDir();
        using var xdg = new TempDir();
        cwd.WriteConfig("rldump.json", """{ "api": { "token": 42 } }""");

        ConfigException ex = Assert.Throws<ConfigException>(() =>
            ConfigLoader.Load(null, cwd.Path, xdg.Path, null));

        Assert.Contains("invalid config file", ex.Message);
    }
}
