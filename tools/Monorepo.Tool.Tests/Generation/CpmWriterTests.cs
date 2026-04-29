using Monorepo.Tool.Generation;
using Xunit;

namespace Monorepo.Tool.Tests.Generation;

public class CpmWriterTests
{
    [Fact]
    public void Write_creates_Directory_Packages_props_with_ManagePackageVersionsCentrally()
    {
        using var fx = new TempRepoFixture();
        var versions = new Dictionary<string, string>
        {
            ["Foo.Bar"] = "2.3.1",
            ["Baz.Qux"] = "1.0.0",
        };

        CpmWriter.Write(fx.Root, versions, dryRun: false);

        var path = Path.Combine(fx.Root, "Directory.Packages.props");
        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("ManagePackageVersionsCentrally", content);
        Assert.Contains("true", content);
    }

    [Fact]
    public void Write_includes_PackageVersion_entries_sorted_alphabetically()
    {
        using var fx = new TempRepoFixture();
        var versions = new Dictionary<string, string>
        {
            ["Zzz.Last"]  = "1.0.0",
            ["Aaa.First"] = "2.0.0",
            ["Mmm.Mid"]   = "3.0.0",
        };

        CpmWriter.Write(fx.Root, versions, dryRun: false);

        var content = File.ReadAllText(Path.Combine(fx.Root, "Directory.Packages.props"));
        var aIdx = content.IndexOf("Aaa.First",  StringComparison.Ordinal);
        var mIdx = content.IndexOf("Mmm.Mid",    StringComparison.Ordinal);
        var zIdx = content.IndexOf("Zzz.Last",   StringComparison.Ordinal);

        Assert.True(aIdx < mIdx && mIdx < zIdx, "entries must be alphabetically sorted");
        Assert.Contains("Version=\"2.0.0\"", content); // Aaa.First
        Assert.Contains("Version=\"3.0.0\"", content); // Mmm.Mid
        Assert.Contains("Version=\"1.0.0\"", content); // Zzz.Last
    }

    [Fact]
    public void Write_is_idempotent()
    {
        using var fx = new TempRepoFixture();
        var versions = new Dictionary<string, string> { ["Foo"] = "1.0.0" };

        CpmWriter.Write(fx.Root, versions, dryRun: false);
        var mtime1 = File.GetLastWriteTimeUtc(Path.Combine(fx.Root, "Directory.Packages.props"));

        CpmWriter.Write(fx.Root, versions, dryRun: false);
        var mtime2 = File.GetLastWriteTimeUtc(Path.Combine(fx.Root, "Directory.Packages.props"));

        Assert.Equal(mtime1, mtime2);
    }

    [Fact]
    public void Write_dryRun_does_not_create_file()
    {
        using var fx = new TempRepoFixture();
        var versions = new Dictionary<string, string> { ["Foo"] = "1.0.0" };

        CpmWriter.Write(fx.Root, versions, dryRun: true);

        Assert.False(File.Exists(Path.Combine(fx.Root, "Directory.Packages.props")));
    }
}
