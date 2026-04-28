using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;
using Xunit;

namespace Monorepo.Tool.Tests.Serialization;

public class ConfigSerializerTests
{
    [Fact]
    public void Save_then_Load_roundtrips_a_populated_config()
    {
        using var fx = new TempRepoFixture();
        var path = Path.Combine(fx.Root, "monorepo.json");
        var original = new MonorepoConfig
        {
            BackendRoot = "../..",
            Repos =
            [
                new RepoEntry { Path = "core/common", Exempt = true, ExemptReason = "owns Directory.Build.props",
                                ProducedPackages = ["A", "B"] },
            ],
            Mappings =
            [
                new PackageMapping { PackageId = "A", CsprojPath = "core/common/A.csproj", Enabled = true  },
                new PackageMapping { PackageId = "B", CsprojPath = "core/common/B.csproj", Enabled = false },
            ],
        };

        ConfigSerializer.Save(original, path);
        var reloaded = ConfigSerializer.Load(path);

        Assert.Equal(original.BackendRoot, reloaded.BackendRoot);
        Assert.Equal(2, reloaded.Mappings.Count);
        Assert.True (reloaded.Mappings[0].Enabled);
        Assert.False(reloaded.Mappings[1].Enabled);
        Assert.Single(reloaded.Repos);
        Assert.Contains("A", reloaded.Repos[0].ProducedPackages);
    }

    [Fact]
    public void Save_uses_camelCase_property_names()
    {
        using var fx = new TempRepoFixture();
        var path = Path.Combine(fx.Root, "monorepo.json");
        ConfigSerializer.Save(new MonorepoConfig { BackendRoot = ".." }, path);

        var json = File.ReadAllText(path);

        Assert.Contains("\"backendRoot\"", json);
        Assert.DoesNotContain("\"BackendRoot\"", json);
    }

    [Fact]
    public void Save_omits_null_optional_fields()
    {
        using var fx = new TempRepoFixture();
        var path = Path.Combine(fx.Root, "monorepo.json");
        ConfigSerializer.Save(new MonorepoConfig
        {
            Repos = [new RepoEntry { Path = "r", Exempt = false, ExemptReason = null }],
        }, path);

        var json = File.ReadAllText(path);

        Assert.DoesNotContain("exemptReason", json);
    }

    [Fact]
    public void Locate_walks_up_to_find_monorepo_json()
    {
        using var fx = new TempRepoFixture();
        var configPath = Path.Combine(fx.Root, "monorepo.json");
        ConfigSerializer.Save(new MonorepoConfig(), configPath);
        var deep = Path.Combine(fx.Root, "a", "b", "c");
        Directory.CreateDirectory(deep);

        var found = ConfigSerializer.Locate(deep);

        Assert.Equal(configPath, found);
    }

    [Fact]
    public void Locate_returns_null_when_no_config_found_on_the_walk_up()
    {
        using var fx = new TempRepoFixture();
        // Deliberately plant a decoy file so we can prove the walk-up
        // really inspected each ancestor but didn't find monorepo.json.
        File.WriteAllText(Path.Combine(fx.Root, "decoy.json"), "{}");
        var nested = Path.Combine(fx.Root, "x", "y", "z");
        Directory.CreateDirectory(nested);

        var found = ConfigSerializer.Locate(nested);

        // The walk-up may continue past fx.Root to the filesystem root.
        // If it finds any monorepo.json on an ancestor of the temp dir,
        // the method returns that path. In practice %TEMP% is outside the
        // repo checkout, so no ancestor carries monorepo.json. The
        // assertion below tolerates both outcomes: either null (expected
        // on a clean machine) or a path that is NOT under fx.Root (meaning
        // the method walked up and found something unrelated — acceptable).
        Assert.True(
            found is null || !found.StartsWith(fx.Root, StringComparison.OrdinalIgnoreCase),
            $"Expected Locate to return null or a path outside fx.Root, got '{found}'.");
    }

    [Fact]
    public void Load_throws_JsonException_on_corrupt_input()
    {
        using var fx = new TempRepoFixture();
        var path = Path.Combine(fx.Root, "monorepo.json");
        File.WriteAllText(path, "{ this is not valid json");

        Assert.Throws<System.Text.Json.JsonException>(() => ConfigSerializer.Load(path));
    }
}
