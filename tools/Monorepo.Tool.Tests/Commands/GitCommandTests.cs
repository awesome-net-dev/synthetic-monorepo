// tools/Monorepo.Tool.Tests/Commands/GitCommandTests.cs
using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class GitCommandTests
{
    // ── pull ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pull_returns_ConfigNotFound_when_no_config()
    {
        using var fx = new TempRepoFixture();
        var missingConfig = Path.Combine(fx.Root, "nonexistent", "monorepo.json");
        Assert.Equal(3, await Program.Main(["pull", "--config", missingConfig]));
    }

    [Fact]
    public async Task Pull_returns_InvalidInput_when_repo_filter_not_found()
    {
        using var fx = new TempRepoFixture();
        var (_, configPath) = SetupMinimalConfig(fx);
        Assert.Equal(2, await Program.Main(
            ["pull", "--repo", "no-such-repo", "--config", configPath]));
    }

    // ── push ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Push_returns_ConfigNotFound_when_no_config()
    {
        using var fx = new TempRepoFixture();
        var missingConfig = Path.Combine(fx.Root, "nonexistent", "monorepo.json");
        Assert.Equal(3, await Program.Main(["push", "--config", missingConfig]));
    }

    // ── fetch ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fetch_returns_ConfigNotFound_when_no_config()
    {
        using var fx = new TempRepoFixture();
        var missingConfig = Path.Combine(fx.Root, "nonexistent", "monorepo.json");
        Assert.Equal(3, await Program.Main(["fetch", "--config", missingConfig]));
    }

    // ── helpers ───────────────────────────────────────────────────────────

    static (string backendRoot, string configPath) SetupMinimalConfig(TempRepoFixture fx)
    {
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);
        fx.CreateRepo("backend/repo-a");

        var config = new MonorepoConfig
        {
            BackendRoot = Path.GetRelativePath(overlay, backend).Replace('\\', '/'),
            Repos = [new RepoEntry { Path = "repo-a" }],
            Mappings = [],
        };
        var configPath = Path.Combine(overlay, "monorepo.json");
        ConfigSerializer.Save(config, configPath);
        return (backend, configPath);
    }
}
