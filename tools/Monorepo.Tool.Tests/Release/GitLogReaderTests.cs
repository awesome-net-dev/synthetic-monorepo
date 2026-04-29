using System.Diagnostics;
using Monorepo.Tool.Release;
using Xunit;

namespace Monorepo.Tool.Tests.Release;

public class GitLogReaderTests
{
    private static string InitRepo(string path)
    {
        Directory.CreateDirectory(path);
        Git(path, "init -b main");
        Git(path, "config user.email test@example.com");
        Git(path, "config user.name  Test");
        Git(path, "commit --allow-empty -m \"chore: initial commit\"");
        return path;
    }

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

    private static void GitCommit(string repoPath, string message)
    {
        Git(repoPath, $"commit --allow-empty -m \"{message}\"");
    }

    private static void GitTag(string repoPath, string tag)
    {
        Git(repoPath, $"tag {tag}");
    }

    [Fact]
    public void GetLatestTag_returns_most_recent_v_tag()
    {
        using var fx = new TempRepoFixture();
        var repo = InitRepo(Path.Combine(fx.Root, "myrepo"));
        GitTag(repo,    "v0.1.0");
        GitCommit(repo, "feat: something");
        GitTag(repo,    "v0.2.0");

        var tag = GitLogReader.GetLatestTag(repo);
        Assert.Equal("v0.2.0", tag);
    }

    [Fact]
    public void GetLatestTag_returns_null_when_no_tags()
    {
        using var fx = new TempRepoFixture();
        var repo = InitRepo(Path.Combine(fx.Root, "myrepo"));
        Assert.Null(GitLogReader.GetLatestTag(repo));
    }

    [Fact]
    public void GetCommitsSinceTag_returns_commits_after_tag()
    {
        using var fx = new TempRepoFixture();
        var repo = InitRepo(Path.Combine(fx.Root, "myrepo"));
        GitTag(repo,    "v1.0.0");
        GitCommit(repo, "feat: after tag one");
        GitCommit(repo, "fix: after tag two");

        var commits = GitLogReader.GetCommitsSinceTag(repo, "v1.0.0");

        Assert.Equal(2, commits.Count);
        Assert.Contains(commits, c => c.Subject == "feat: after tag one");
        Assert.Contains(commits, c => c.Subject == "fix: after tag two");
    }

    [Fact]
    public void GetCommitsSinceTag_returns_all_commits_when_sinceTag_is_null()
    {
        using var fx = new TempRepoFixture();
        var repo = InitRepo(Path.Combine(fx.Root, "myrepo"));
        GitCommit(repo, "feat: first");
        GitCommit(repo, "fix: second");

        var commits = GitLogReader.GetCommitsSinceTag(repo, sinceTag: null);

        Assert.True(commits.Count >= 2);
    }

    [Fact]
    public void GetCommitsSinceTag_returns_empty_when_no_commits_since_tag()
    {
        using var fx = new TempRepoFixture();
        var repo = InitRepo(Path.Combine(fx.Root, "myrepo"));
        GitTag(repo, "v1.0.0");

        var commits = GitLogReader.GetCommitsSinceTag(repo, "v1.0.0");
        Assert.Empty(commits);
    }
}
