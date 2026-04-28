using Monorepo.Tool.Discovery;
using Xunit;

namespace Monorepo.Tool.Tests.Discovery;

public class RepoScannerTests
{
    [Fact]
    public void Detects_depth_1_git_repo()
    {
        using var fx = new TempRepoFixture();
        fx.CreateRepo("solo");

        var result = RepoScanner.Scan(fx.Root);

        Assert.Single(result);
        Assert.Equal("solo", result[0].Path);
        Assert.False(result[0].Exempt);
    }

    [Fact]
    public void Detects_depth_2_git_repo_under_grouping_dir()
    {
        using var fx = new TempRepoFixture();
        // depth-1 dir is not a repo (no .git); depth-2 is
        Directory.CreateDirectory(Path.Combine(fx.Root, "billing"));
        fx.CreateRepo("billing/payments");

        var result = RepoScanner.Scan(fx.Root);

        Assert.Single(result);
        Assert.Equal("billing/payments", result[0].Path);
    }

    [Fact]
    public void Flags_repo_with_its_own_Directory_Build_props_as_exempt()
    {
        using var fx = new TempRepoFixture();
        fx.CreateRepo("fancy", ownsDirectoryBuildProps: true);

        var result = RepoScanner.Scan(fx.Root);

        Assert.Single(result);
        Assert.True(result[0].Exempt);
        Assert.Equal("owns Directory.Build.props", result[0].ExemptReason);
    }

    [Fact]
    public void Treats_dot_git_as_file_worktree_like_a_regular_repo()
    {
        using var fx = new TempRepoFixture();
        var dir = Path.Combine(fx.Root, "worktree");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".git"), "gitdir: /some/main/clone/.git/worktrees/wt");

        var result = RepoScanner.Scan(fx.Root);

        Assert.Single(result);
        Assert.Equal("worktree", result[0].Path);
    }

    [Fact]
    public void Skips_bin_and_obj_at_depth_1_even_if_they_contain_dot_git()
    {
        using var fx = new TempRepoFixture();
        // Simulate: a stray .git inside bin/ (e.g., from a cached clone)
        var binRepo = Path.Combine(fx.Root, "bin", "cached");
        Directory.CreateDirectory(Path.Combine(binRepo, ".git"));
        // A real repo at depth 1 to prove scanning still works
        fx.CreateRepo("real");

        var result = RepoScanner.Scan(fx.Root);

        Assert.Single(result);
        Assert.Equal("real", result[0].Path);
    }

    [Fact]
    public void Throws_when_backend_root_does_not_exist()
    {
        var missing = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<DirectoryNotFoundException>(() => RepoScanner.Scan(missing));
    }

    [Fact]
    public void Skips_directories_with_the_hidden_file_attribute()
    {
        using var fx = new TempRepoFixture();
        // This test verifies ShouldSkip's Hidden-attribute branch. Caveat by OS:
        //   Windows: File.SetAttributes stores the flag directly — this test
        //            exercises the (dir.Attributes & Hidden) == Hidden check.
        //   Linux/macOS: FileAttributes.Hidden is derived from dot-prefix filenames;
        //            File.SetAttributes is a no-op on directories. The skip happens
        //            because ".ghost" has a dot prefix, not because of the explicit
        //            SetAttributes call — so on Unix this test covers the same branch
        //            incidentally via the same code path (attribute bit check).
        var hidden = Path.Combine(fx.Root, ".ghost");
        Directory.CreateDirectory(hidden);
        Directory.CreateDirectory(Path.Combine(hidden, ".git"));
        File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);

        // A real repo to confirm scanning still works alongside the hidden one
        fx.CreateRepo("real");

        var result = RepoScanner.Scan(fx.Root);

        Assert.Single(result);
        Assert.Equal("real", result[0].Path);
    }
}
