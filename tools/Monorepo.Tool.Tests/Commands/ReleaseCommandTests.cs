using System.Diagnostics;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class ReleaseCommandTests
{
    private static void Git(string repoPath, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
    }

    private static void InitGitRepo(string path)
    {
        Directory.CreateDirectory(path);
        Git(path, "init -b main");
        Git(path, "config user.email test@example.com");
        Git(path, "config user.name  Test");
        Git(path, "commit --allow-empty -m \"chore: init\"");
    }

    [Fact]
    public async Task Release_dryRun_writes_nothing_and_creates_no_tag()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);

        var repoPath = Path.Combine(backend, "myrepo");
        InitGitRepo(repoPath);
        Git(repoPath, "tag v1.0.0");
        Git(repoPath, "commit --allow-empty -m \"feat: new feature\"");

        Assert.Equal(0, await Program.Main(
            ["init", "--backend", backend, "--overlay", overlay]));

        var configPath = Path.Combine(overlay, "monorepo.json");
        Assert.Equal(0, await Program.Main(
            ["release", "--repo", "myrepo", "--config", configPath, "--dry-run"]));

        Assert.False(File.Exists(Path.Combine(repoPath, "CHANGELOG.md")));

        // Also verify no tag was created
        var psi2 = new ProcessStartInfo("git", "tag --list v1.1.0")
        {
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            UseShellExecute        = false,
        };
        using var p2 = Process.Start(psi2)!;
        var tagOutput = p2.StandardOutput.ReadToEnd().Trim();
        p2.WaitForExit();
        Assert.Equal("", tagOutput); // tag must NOT exist
    }

    [Fact]
    public async Task Release_writes_changelog_and_creates_tag()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);

        var repoPath = Path.Combine(backend, "myrepo");
        InitGitRepo(repoPath);
        Git(repoPath, "tag v1.2.0");
        Git(repoPath, "commit --allow-empty -m \"feat: alpha\"");
        Git(repoPath, "commit --allow-empty -m \"fix: beta\"");

        Assert.Equal(0, await Program.Main(
            ["init", "--backend", backend, "--overlay", overlay]));

        var configPath = Path.Combine(overlay, "monorepo.json");
        Assert.Equal(0, await Program.Main(
            ["release", "--repo", "myrepo", "--config", configPath]));

        var changelog = File.ReadAllText(Path.Combine(repoPath, "CHANGELOG.md"));
        Assert.Contains("## [1.3.0]", changelog); // feat → minor bump from 1.2.0
        Assert.Contains("alpha",      changelog);
        Assert.Contains("beta",       changelog);

        var psi = new ProcessStartInfo("git", "tag --list v1.3.0")
        {
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            UseShellExecute        = false,
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd().Trim();
        p.WaitForExit();
        Assert.Equal("v1.3.0", output);
    }
}
