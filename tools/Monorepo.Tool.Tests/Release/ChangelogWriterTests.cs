using Monorepo.Tool.Releases;
using Xunit;

namespace Monorepo.Tool.Tests.Release;

public class ChangelogWriterTests
{
    private static readonly DateTime TestDate = new(2026, 4, 28);

    [Fact]
    public void Write_creates_changelog_with_version_header()
    {
        using var fx = new TempRepoFixture();
        var commits = new List<ConventionalCommit>
        {
            new("feat", null, false, "add cpm command"),
            new("fix",  null, false, "handle empty versions"),
        };

        ChangelogWriter.Write(fx.Root, "1.0.0", commits, TestDate, dryRun: false);

        var content = File.ReadAllText(Path.Combine(fx.Root, "CHANGELOG.md"));
        Assert.Contains("## [1.0.0] - 2026-04-28", content);
        Assert.Contains("### Added",   content);
        Assert.Contains("add cpm command", content);
        Assert.Contains("### Fixed",   content);
        Assert.Contains("handle empty versions", content);
    }

    [Fact]
    public void Write_prepends_to_existing_changelog()
    {
        using var fx = new TempRepoFixture();
        var existing = "## [0.9.0] - 2026-01-01\n\n- old entry\n";
        File.WriteAllText(Path.Combine(fx.Root, "CHANGELOG.md"), existing);

        var commits = new List<ConventionalCommit>
        {
            new("feat", null, false, "new feature"),
        };

        ChangelogWriter.Write(fx.Root, "1.0.0", commits, TestDate, dryRun: false);

        var content = File.ReadAllText(Path.Combine(fx.Root, "CHANGELOG.md"));
        var newIdx  = content.IndexOf("## [1.0.0]",  StringComparison.Ordinal);
        var oldIdx  = content.IndexOf("## [0.9.0]",  StringComparison.Ordinal);
        Assert.True(newIdx < oldIdx, "new entry must appear before old entry");
    }

    [Fact]
    public void Write_groups_commits_into_correct_sections()
    {
        using var fx = new TempRepoFixture();
        var commits = new List<ConventionalCommit>
        {
            new("feat",     null, false, "feat one"),
            new("fix",      null, false, "fix one"),
            new("refactor", null, false, "refactor one"),
            new("chore",    null, false, "chore one"),
        };

        ChangelogWriter.Write(fx.Root, "1.0.0", commits, TestDate, dryRun: false);

        var content = File.ReadAllText(Path.Combine(fx.Root, "CHANGELOG.md"));
        Assert.Contains("### Added",   content);
        Assert.Contains("### Fixed",   content);
        Assert.Contains("### Changed", content);
        Assert.Contains("### Other",   content);
    }

    [Fact]
    public void Write_dryRun_does_not_create_file()
    {
        using var fx = new TempRepoFixture();
        ChangelogWriter.Write(fx.Root, "1.0.0", [], TestDate, dryRun: true);
        Assert.False(File.Exists(Path.Combine(fx.Root, "CHANGELOG.md")));
    }

    [Fact]
    public void Write_omits_empty_sections()
    {
        using var fx = new TempRepoFixture();
        var commits = new List<ConventionalCommit>
        {
            new("feat", null, false, "only a feature"),
        };

        ChangelogWriter.Write(fx.Root, "1.0.0", commits, TestDate, dryRun: false);

        var content = File.ReadAllText(Path.Combine(fx.Root, "CHANGELOG.md"));
        Assert.DoesNotContain("### Fixed",   content);
        Assert.DoesNotContain("### Changed", content);
    }
}
