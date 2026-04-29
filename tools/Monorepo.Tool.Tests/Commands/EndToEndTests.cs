using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class EndToEndTests
{
    [Fact]
    public async Task Full_lifecycle_on_two_repo_backend()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);

        var producerRepo = fx.CreateRepo("backend/producer");
        var consumerRepo = fx.CreateRepo("backend/consumer");
        fx.WriteCsproj(producerRepo, "src/P.csproj",
            "<Project><PropertyGroup><PackageId>Shared.Lib</PackageId></PropertyGroup></Project>");
        fx.WriteCsproj(consumerRepo, "src/C.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Shared.Lib" Version="1.0" />
              </ItemGroup>
            </Project>
            """);

        // 1. init
        Assert.Equal(0, await Program.Main(["init", "--backend", backend, "--overlay", overlay]));
        var configPath = Path.Combine(overlay, "monorepo.json");
        Assert.True(File.Exists(configPath));
        var cfg = ConfigSerializer.Load(configPath);
        Assert.Equal(2, cfg.Repos.Count);
        var mapping = Assert.Single(cfg.Mappings);
        Assert.Equal("Shared.Lib", mapping.PackageId);

        // Sentinel active after init
        Assert.True(File.Exists(Path.Combine(backend, ".monorepo-active")));
        // Shims exist
        Assert.True(File.Exists(Path.Combine(backend, "Directory.Build.props")));
        Assert.True(File.Exists(Path.Combine(backend, "Directory.Build.targets")));
        // Overlay files exist
        Assert.True(File.Exists(Path.Combine(overlay, "overlay", "Directory.Build.props")));
        Assert.True(File.Exists(Path.Combine(overlay, "overlay", "Directory.Build.targets")));

        // 2. off / on toggle
        Assert.Equal(0, await Program.Main(["off", "--config", configPath]));
        Assert.False(File.Exists(Path.Combine(backend, ".monorepo-active")));
        Assert.Equal(0, await Program.Main(["on",  "--config", configPath]));
        Assert.True (File.Exists(Path.Combine(backend, ".monorepo-active")));

        // 3. disable + regenerate → mapping removed from targets
        Assert.Equal(0, await Program.Main(["disable", "Shared.Lib", "--config", configPath]));
        Assert.Equal(0, await Program.Main(["generate", "--config", configPath]));
        var targets = File.ReadAllText(Path.Combine(overlay, "overlay", "Directory.Build.targets"));
        Assert.DoesNotContain("<ProjectReference", targets);

        // 4. enable again → mapping injected
        Assert.Equal(0, await Program.Main(["enable", "Shared.Lib", "--config", configPath]));
        Assert.Equal(0, await Program.Main(["generate", "--config", configPath]));
        targets = File.ReadAllText(Path.Combine(overlay, "overlay", "Directory.Build.targets"));
        Assert.Contains("<ProjectReference", targets);

        // 5. status with no drift returns 0
        Assert.Equal(0, await Program.Main(["status", "--config", configPath]));
    }
}
