using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class EnableDisableTests
{
    [Fact]
    public async Task Disable_sets_Enabled_false_for_matching_package()
    {
        using var fx = SetupConfig(enabled: true);
        var exit = await Monorepo.Tool.Program.Main(
            ["disable", "Foo.Bar", "--config", fx.ConfigPath]);
        Assert.Equal(0, exit);

        var reloaded = ConfigSerializer.Load(fx.ConfigPath);
        Assert.False(reloaded.Mappings.Single().Enabled);
    }

    [Fact]
    public async Task Enable_sets_Enabled_true_for_matching_package()
    {
        using var fx = SetupConfig(enabled: false);
        var exit = await Monorepo.Tool.Program.Main(
            ["enable", "Foo.Bar", "--config", fx.ConfigPath]);
        Assert.Equal(0, exit);

        var reloaded = ConfigSerializer.Load(fx.ConfigPath);
        Assert.True(reloaded.Mappings.Single().Enabled);
    }

    [Fact]
    public async Task Disable_returns_nonzero_when_package_is_unknown()
    {
        using var fx = SetupConfig(enabled: true);
        var exit = await Monorepo.Tool.Program.Main(
            ["disable", "Not.A.Package", "--config", fx.ConfigPath]);
        Assert.NotEqual(0, exit);
    }

    private static ConfigHarness SetupConfig(bool enabled)
    {
        var fx = new TempRepoFixture();
        var configPath = Path.Combine(fx.Root, "monorepo.json");
        ConfigSerializer.Save(new MonorepoConfig
        {
            BackendRoot = "..",
            Mappings = [new PackageMapping { PackageId = "Foo.Bar", CsprojPath = "a/A.csproj", Enabled = enabled }],
        }, configPath);
        return new ConfigHarness(fx, configPath);
    }

    private sealed class ConfigHarness(TempRepoFixture fx, string configPath) : IDisposable
    {
        public string ConfigPath { get; } = configPath;
        public void Dispose() => fx.Dispose();
    }
}
