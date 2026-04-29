using Monorepo.Tool.Releases;
using Xunit;

namespace Monorepo.Tool.Tests.Release;

public class ConventionalCommitParserTests
{
    [Fact]
    public void Parse_feat_subject_returns_feat_type()
    {
        var result = ConventionalCommitParser.Parse("feat: add new command", body: null);
        Assert.NotNull(result);
        Assert.Equal("feat", result.Type);
        Assert.False(result.Breaking);
        Assert.Equal("add new command", result.Description);
    }

    [Fact]
    public void Parse_fix_with_scope()
    {
        var result = ConventionalCommitParser.Parse("fix(cpm): handle empty version", body: null);
        Assert.NotNull(result);
        Assert.Equal("fix",  result.Type);
        Assert.Equal("cpm",  result.Scope);
        Assert.False(result.Breaking);
    }

    [Fact]
    public void Parse_breaking_via_exclamation_mark()
    {
        var result = ConventionalCommitParser.Parse("feat!: redesign config schema", body: null);
        Assert.NotNull(result);
        Assert.True(result.Breaking);
    }

    [Fact]
    public void Parse_breaking_via_footer_in_body()
    {
        var result = ConventionalCommitParser.Parse(
            "refactor: rework overlay generation",
            body: "BREAKING CHANGE: overlay dir layout has changed");
        Assert.NotNull(result);
        Assert.True(result.Breaking);
    }

    [Fact]
    public void Parse_non_conventional_returns_null()
    {
        Assert.Null(ConventionalCommitParser.Parse("Update readme", body: null));
        Assert.Null(ConventionalCommitParser.Parse("WIP", body: null));
    }

    [Fact]
    public void Parse_chore_type_is_recognised()
    {
        var result = ConventionalCommitParser.Parse("chore: bump deps", body: null);
        Assert.NotNull(result);
        Assert.Equal("chore", result.Type);
    }
}
