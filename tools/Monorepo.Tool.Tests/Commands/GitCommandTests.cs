// tools/Monorepo.Tool.Tests/Commands/GitCommandTests.cs
using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class GitCommandTests
{
    // ── pull ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pull_returns_GeneralError_when_config_path_invalid()
    {
        using var fx = new TempRepoFixture();
        var missingConfig = Path.Combine(fx.Root, "nonexistent", "monorepo.json");
        Assert.Equal(1, await Program.Main(["pull", "--config", missingConfig]));
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
    public async Task Push_returns_GeneralError_when_config_path_invalid()
    {
        using var fx = new TempRepoFixture();
        var missingConfig = Path.Combine(fx.Root, "nonexistent", "monorepo.json");
        Assert.Equal(1, await Program.Main(["push", "--config", missingConfig]));
    }

    [Fact]
    public async Task Push_returns_InvalidInput_when_repo_filter_not_found()
    {
        using var fx = new TempRepoFixture();
        var (_, configPath) = SetupMinimalConfig(fx);
        Assert.Equal(2, await Program.Main(
            ["push", "--repo", "no-such-repo", "--config", configPath]));
    }

    [Fact]
    public async Task Fetch_returns_InvalidInput_when_repo_filter_not_found()
    {
        using var fx = new TempRepoFixture();
        var (_, configPath) = SetupMinimalConfig(fx);
        Assert.Equal(2, await Program.Main(
            ["fetch", "--repo", "no-such-repo", "--config", configPath]));
    }

    // ── fetch ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fetch_returns_GeneralError_when_config_path_invalid()
    {
        using var fx = new TempRepoFixture();
        var missingConfig = Path.Combine(fx.Root, "nonexistent", "monorepo.json");
        Assert.Equal(1, await Program.Main(["fetch", "--config", missingConfig]));
    }

    // ── sync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sync_returns_GeneralError_when_config_path_invalid()
    {
        using var fx = new TempRepoFixture();
        var missingConfig = Path.Combine(fx.Root, "nonexistent", "monorepo.json");
        Assert.Equal(1, await Program.Main(["sync", "--config", missingConfig]));
    }

    [Fact]
    public async Task Sync_runs_without_error_on_local_repos()
    {
        using var fx = new TempRepoFixture();
        var (_, configPath) = SetupMinimalConfig(fx);
        // git pull will fail on repos with no remote — sync should complete the
        // refresh phase and return GeneralError (1), not crash
        var exit = await Program.Main(["sync", "--config", configPath]);
        Assert.True(exit == 0 || exit == 1,
            $"Expected 0 or 1, got {exit}");
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
