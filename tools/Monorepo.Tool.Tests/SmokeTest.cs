using System.IO;
using Monorepo.Tool.Model;
using Xunit;

namespace Monorepo.Tool.Tests;

public class SmokeTest
{
    [Fact]
    public void MonorepoConfig_defaults_are_sane()
    {
        var c = new MonorepoConfig();

        Assert.Equal(1, c.Version);
        Assert.Equal("../..", c.BackendRoot);
        Assert.Empty(c.Repos);
        Assert.Empty(c.Mappings);
    }
}

public class TempRepoFixtureSmoke
{
    [Fact]
    public void Fixture_creates_and_disposes_temp_directory()
    {
        string capturedPath;
        using (var fx = new TempRepoFixture())
        {
            capturedPath = fx.Root;
            Assert.True(Directory.Exists(capturedPath));
        }
        Assert.False(Directory.Exists(capturedPath));
    }
}
