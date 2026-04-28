using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class GenerateRefreshTests
{
    [Fact]
    public async Task Refresh_preserves_Enabled_false_overrides_on_existing_mappings()
    {
        using var fx = new TempRepoFixture();
        var (backend, overlay, configPath) = await InitFromProducerConsumer(fx, "Shared.Lib");

        // User manually disables
        Assert.Equal(0, await Monorepo.Tool.Program.Main(["disable", "Shared.Lib", "--config", configPath]));

        // generate --refresh should NOT re-enable
        Assert.Equal(0, await Monorepo.Tool.Program.Main(["generate", "--refresh", "--config", configPath]));

        var cfg = ConfigSerializer.Load(configPath);
        Assert.False(cfg.Mappings.Single(m => m.PackageId == "Shared.Lib").Enabled);
    }

    [Fact]
    public async Task Refresh_adds_newly_discovered_cross_repo_mapping()
    {
        using var fx = new TempRepoFixture();
        var (backend, overlay, configPath) = await InitFromProducerConsumer(fx, "Shared.Lib");

        // Add another cross-repo mapping on disk
        var newProducer = fx.CreateRepo("backend/extra");
        fx.WriteCsproj(newProducer, "src/E.csproj",
            "<Project><PropertyGroup><PackageId>Extra.Lib</PackageId></PropertyGroup></Project>");
        var consumerDir = Path.Combine(backend, "consumer", "src");
        File.WriteAllText(Path.Combine(consumerDir, "C.csproj"), """
            <Project>
              <ItemGroup>
                <PackageReference Include="Shared.Lib" Version="1.0" />
                <PackageReference Include="Extra.Lib"  Version="1.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal(0, await Monorepo.Tool.Program.Main(["generate", "--refresh", "--config", configPath]));

        var cfg = ConfigSerializer.Load(configPath);
        Assert.Contains(cfg.Mappings, m => m.PackageId == "Extra.Lib"   && m.Enabled);
        Assert.Contains(cfg.Mappings, m => m.PackageId == "Shared.Lib");
    }

    [Fact]
    public async Task Refresh_drops_mapping_when_producing_csproj_disappears()
    {
        using var fx = new TempRepoFixture();
        var (backend, overlay, configPath) = await InitFromProducerConsumer(fx, "Shared.Lib");

        // Delete the producer csproj
        File.Delete(Path.Combine(backend, "producer", "src", "P.csproj"));

        Assert.Equal(0, await Monorepo.Tool.Program.Main(["generate", "--refresh", "--config", configPath]));

        var cfg = ConfigSerializer.Load(configPath);
        Assert.DoesNotContain(cfg.Mappings, m => m.PackageId == "Shared.Lib");
    }

    private static async Task<(string backend, string overlay, string configPath)> InitFromProducerConsumer(
        TempRepoFixture fx, string packageId)
    {
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);

        var producer = fx.CreateRepo("backend/producer");
        var consumer = fx.CreateRepo("backend/consumer");
        fx.WriteCsproj(producer, "src/P.csproj",
            $"<Project><PropertyGroup><PackageId>{packageId}</PackageId></PropertyGroup></Project>");
        fx.WriteCsproj(consumer, "src/C.csproj", $"""
            <Project>
              <ItemGroup>
                <PackageReference Include="{packageId}" Version="1.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal(0, await Monorepo.Tool.Program.Main(["init", "--backend", backend, "--overlay", overlay]));
        return (backend, overlay, Path.Combine(overlay, "monorepo.json"));
    }
}
