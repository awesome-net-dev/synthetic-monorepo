using Monorepo.Tool.Generation;
using Monorepo.Tool.Model;
using Xunit;

namespace Monorepo.Tool.Tests.Generation;

public class OverlayWriterTests
{
    [Fact]
    public void Writes_both_props_and_targets_to_overlay_dir()
    {
        using var fx = new TempRepoFixture();
        var overlay = Path.Combine(fx.Root, "overlay");

        OverlayWriter.Write(overlay, fx.Root, []);

        Assert.True(File.Exists(Path.Combine(overlay, "Directory.Build.props")));
        Assert.True(File.Exists(Path.Combine(overlay, "Directory.Build.targets")));
    }

    [Fact]
    public void Zero_mappings_produces_targets_without_ProjectReference_elements()
    {
        using var fx = new TempRepoFixture();
        var overlay = Path.Combine(fx.Root, "overlay");

        OverlayWriter.Write(overlay, fx.Root, []);

        var targets = File.ReadAllText(Path.Combine(overlay, "Directory.Build.targets"));
        Assert.DoesNotContain("<ProjectReference", targets);
    }

    [Fact]
    public void Disabled_mappings_produce_no_ProjectReference_elements()
    {
        using var fx = new TempRepoFixture();
        var overlay = Path.Combine(fx.Root, "overlay");
        var mappings = new List<PackageMapping>
        {
            new() { PackageId = "Foo", CsprojPath = "repo/Foo.csproj", Enabled = false },
        };

        OverlayWriter.Write(overlay, fx.Root, mappings);

        var targets = File.ReadAllText(Path.Combine(overlay, "Directory.Build.targets"));
        Assert.DoesNotContain("<ProjectReference", targets);
    }

    [Fact]
    public void Enabled_mapping_emits_snapshot_remove_and_inject_layers()
    {
        using var fx = new TempRepoFixture();
        var overlay = Path.Combine(fx.Root, "overlay");
        var mappings = new List<PackageMapping>
        {
            new() { PackageId = "Foo.Bar", CsprojPath = "repo/Foo.Bar.csproj", Enabled = true },
        };

        OverlayWriter.Write(overlay, fx.Root, mappings);

        var targets = File.ReadAllText(Path.Combine(overlay, "Directory.Build.targets"));

        // Layer 1a: snapshot of PackageReferences
        Assert.Contains("_MonorepoOriginalPackageReference", targets);
        // Layer 1b: removal
        Assert.Contains("Remove=\"Foo.Bar\"", targets);
        // Layer 2: injection
        Assert.Contains("<ProjectReference", targets);
        // Producer guard referencing the producing csproj
        Assert.Contains("Foo.Bar.csproj", targets);
    }

    [Fact]
    public void Regenerating_unchanged_content_is_idempotent_on_disk()
    {
        using var fx = new TempRepoFixture();
        var overlay = Path.Combine(fx.Root, "overlay");
        var mappings = new List<PackageMapping>
        {
            new() { PackageId = "Foo", CsprojPath = "repo/Foo.csproj", Enabled = true },
        };

        OverlayWriter.Write(overlay, fx.Root, mappings);
        var firstWrite = File.GetLastWriteTimeUtc(Path.Combine(overlay, "Directory.Build.targets"));

        // Small delay so any write would show up as a different timestamp
        Thread.Sleep(50);

        OverlayWriter.Write(overlay, fx.Root, mappings);
        var secondWrite = File.GetLastWriteTimeUtc(Path.Combine(overlay, "Directory.Build.targets"));

        Assert.Equal(firstWrite, secondWrite);
    }
}
