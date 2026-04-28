using Monorepo.Tool.Generation;
using Monorepo.Tool.IO;
using Monorepo.Tool.Model;
using Xunit;

namespace Monorepo.Tool.Tests.Generation;

public class DryRunTests
{
    [Fact]
    public void AtomicFile_dry_run_does_not_create_the_target_file()
    {
        using var fx = new TempRepoFixture();
        var target = Path.Combine(fx.Root, "out.txt");

        var wrote = AtomicFile.WriteAllTextIfChanged(target, "hello", dryRun: true);

        Assert.False(wrote);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void AtomicFile_dry_run_does_not_overwrite_existing_content()
    {
        using var fx = new TempRepoFixture();
        var target = Path.Combine(fx.Root, "out.txt");
        File.WriteAllText(target, "stale");

        var wrote = AtomicFile.WriteAllTextIfChanged(target, "fresh", dryRun: true);

        Assert.False(wrote);
        Assert.Equal("stale", File.ReadAllText(target));
    }

    [Fact]
    public void OverlayWriter_dry_run_does_not_create_overlay_files()
    {
        using var fx = new TempRepoFixture();
        var overlay = Path.Combine(fx.Root, "overlay");
        var mappings = new List<PackageMapping>
        {
            new() { PackageId = "Foo", CsprojPath = "repo/Foo.csproj", Enabled = true },
        };

        OverlayWriter.Write(overlay, fx.Root, mappings, dryRun: true);

        // Directory may or may not be created by Directory.CreateDirectory at the top
        // of OverlayWriter.Write, but the two overlay files must not exist.
        Assert.False(File.Exists(Path.Combine(overlay, "Directory.Build.props")));
        Assert.False(File.Exists(Path.Combine(overlay, "Directory.Build.targets")));
    }

    [Fact]
    public void ShimWriter_dry_run_does_not_create_shim_files()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);

        ShimWriter.Write(backend, overlay, dryRun: true);

        Assert.False(File.Exists(Path.Combine(backend, "Directory.Build.props")));
        Assert.False(File.Exists(Path.Combine(backend, "Directory.Build.targets")));
    }

    [Fact]
    public void SolutionWriter_dry_run_does_not_create_sln()
    {
        using var fx = new TempRepoFixture();
        var slnPath = Path.Combine(fx.Root, "Monorepo.sln");
        var repos = new List<RepoEntry>();

        SolutionWriter.Write(slnPath, fx.Root, repos, dryRun: true);

        Assert.False(File.Exists(slnPath));
    }
}
