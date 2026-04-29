using Monorepo.Tool.IO;
using Monorepo.Tool.Model;
using Xunit;

namespace Monorepo.Tool.Tests.IO;

public class GitMultiRepoRunnerTests
{
    [Fact]
    public async Task Returns_error_result_for_missing_directory()
    {
        var repos = new[] { new RepoEntry { Path = "no-such-repo" } };
        var results = await GitMultiRepoRunner.RunAsync(
            repos, "/tmp/no-such-backend", ["git", "--version"]);
        var r = Assert.Single(results);
        Assert.Equal(-1, r.ExitCode);
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Runs_command_in_each_repo_sequentially()
    {
        using var fx = new TempRepoFixture();
        fx.CreateRepo("backend/repo-a");
        fx.CreateRepo("backend/repo-b");
        var backendRoot = Path.Combine(fx.Root, "backend");
        var repos = new[]
        {
            new RepoEntry { Path = "repo-a" },
            new RepoEntry { Path = "repo-b" },
        };

        var results = await GitMultiRepoRunner.RunAsync(
            repos, backendRoot, ["git", "--version"]);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
        Assert.All(results, r => Assert.Contains("git version", r.Stdout));
    }

    [Fact]
    public async Task Runs_command_in_parallel()
    {
        using var fx = new TempRepoFixture();
        fx.CreateRepo("backend/repo-a");
        fx.CreateRepo("backend/repo-b");
        var backendRoot = Path.Combine(fx.Root, "backend");
        var repos = new[]
        {
            new RepoEntry { Path = "repo-a" },
            new RepoEntry { Path = "repo-b" },
        };

        var results = await GitMultiRepoRunner.RunAsync(
            repos, backendRoot, ["git", "--version"], parallel: true);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task Output_property_returns_stdout_when_available()
    {
        using var fx = new TempRepoFixture();
        fx.CreateRepo("backend/repo-a");
        var backendRoot = Path.Combine(fx.Root, "backend");
        var repos = new[] { new RepoEntry { Path = "repo-a" } };

        var results = await GitMultiRepoRunner.RunAsync(
            repos, backendRoot, ["git", "--version"]);

        var r = Assert.Single(results);
        Assert.Equal(r.Stdout, r.Output);
    }
}
