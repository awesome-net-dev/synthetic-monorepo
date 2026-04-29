using System.CommandLine;
using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class InitOverwriteTests
{
    [Fact]
    public async Task Init_refuses_to_overwrite_existing_monorepo_json()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);
        var configPath = Path.Combine(overlay, "monorepo.json");
        var sentinel = new MonorepoConfig { BackendRoot = "sentinel-value" };
        ConfigSerializer.Save(sentinel, configPath);

        var root = Program.BuildRoot();
        var exit = await root.Parse(["init", "--backend", backend, "--overlay", overlay]).InvokeAsync();

        Assert.NotEqual(0, exit);
        var reloaded = ConfigSerializer.Load(configPath);
        Assert.Equal("sentinel-value", reloaded.BackendRoot);
    }

    [Fact]
    public async Task Init_force_overwrites_existing_monorepo_json()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);
        var configPath = Path.Combine(overlay, "monorepo.json");
        ConfigSerializer.Save(new MonorepoConfig { BackendRoot = "sentinel-value" }, configPath);

        var root = Program.BuildRoot();
        var exit = await root.Parse(["init", "--backend", backend, "--overlay", overlay, "--force"]).InvokeAsync();

        Assert.Equal(0, exit);
        var reloaded = ConfigSerializer.Load(configPath);
        Assert.NotEqual("sentinel-value", reloaded.BackendRoot);
    }
}
