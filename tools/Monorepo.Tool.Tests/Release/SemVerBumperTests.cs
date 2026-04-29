using Monorepo.Tool.Release;
using Xunit;

namespace Monorepo.Tool.Tests.Release;

public class SemVerBumperTests
{
    [Theory]
    [InlineData("v1.2.3",   1, 2, 3)]
    [InlineData("1.2.3",    1, 2, 3)]
    [InlineData("v0.0.0",   0, 0, 0)]
    [InlineData("v10.0.1",  10, 0, 1)]
    public void ParseVersion_extracts_major_minor_patch(string tag, int major, int minor, int patch)
    {
        var (maj, min, pat) = SemVerBumper.ParseVersion(tag);
        Assert.Equal(major, maj);
        Assert.Equal(minor, min);
        Assert.Equal(patch, pat);
    }

    [Theory]
    [InlineData(BumpType.Patch, "1.2.3", "1.2.4")]
    [InlineData(BumpType.Minor, "1.2.3", "1.3.0")]
    [InlineData(BumpType.Major, "1.2.3", "2.0.0")]
    [InlineData(BumpType.Patch, "0.0.0", "0.0.1")]
    public void Bump_increments_correct_component(BumpType bump, string current, string expected)
    {
        Assert.Equal(expected, SemVerBumper.Bump(current, bump));
    }

    [Fact]
    public void DetermineFromCommits_breaking_gives_major()
    {
        var commits = new List<ConventionalCommit>
        {
            new("feat", null, Breaking: true, "redesign"),
            new("feat", null, Breaking: false, "add thing"),
        };
        Assert.Equal(BumpType.Major, SemVerBumper.DetermineFromCommits(commits));
    }

    [Fact]
    public void DetermineFromCommits_feat_without_breaking_gives_minor()
    {
        var commits = new List<ConventionalCommit>
        {
            new("fix",  null, false, "fix bug"),
            new("feat", null, false, "add thing"),
        };
        Assert.Equal(BumpType.Minor, SemVerBumper.DetermineFromCommits(commits));
    }

    [Fact]
    public void DetermineFromCommits_only_fix_gives_patch()
    {
        var commits = new List<ConventionalCommit>
        {
            new("fix",   null, false, "fix a"),
            new("chore", null, false, "update deps"),
        };
        Assert.Equal(BumpType.Patch, SemVerBumper.DetermineFromCommits(commits));
    }

    [Fact]
    public void DetermineFromCommits_empty_list_gives_patch()
    {
        Assert.Equal(BumpType.Patch, SemVerBumper.DetermineFromCommits([]));
    }
}
