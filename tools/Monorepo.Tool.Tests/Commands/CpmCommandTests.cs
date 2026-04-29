using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class CpmCommandTests
{
    [Fact]
    public async Task Cpm_writes_Directory_Packages_props_and_strips_version_attrs()
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
                <PackageReference Include="Shared.Lib"      Version="1.0.0" />
                <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal(0, await Program.Main(
            ["init", "--backend", backend, "--overlay", overlay]));

        var configPath = Path.Combine(overlay, "monorepo.json");
        Assert.Equal(0, await Program.Main(
            ["cpm", "--config", configPath]));

        var cpmPath = Path.Combine(backend, "Directory.Packages.props");
        Assert.True(File.Exists(cpmPath));

        var cpmContent = File.ReadAllText(cpmPath);
        Assert.Contains("ManagePackageVersionsCentrally", cpmContent);
        Assert.Contains("Newtonsoft.Json", cpmContent);
        Assert.Contains("Shared.Lib",      cpmContent);

        var consumerCsproj = Path.Combine(consumerRepo, "src", "C.csproj");
        var csprojContent  = File.ReadAllText(consumerCsproj);
        Assert.DoesNotContain("Version=\"1.0.0\"",  csprojContent);
        Assert.DoesNotContain("Version=\"13.0.1\"", csprojContent);
        Assert.Contains("Shared.Lib",      csprojContent);
        Assert.Contains("Newtonsoft.Json", csprojContent);
    }

    [Fact]
    public async Task Cpm_dry_run_writes_nothing()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);

        fx.CreateRepo("backend/consumer");
        fx.WriteCsproj(Path.Combine(backend, "consumer"), "src/C.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Foo" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        Assert.Equal(0, await Program.Main(
            ["init", "--backend", backend, "--overlay", overlay]));

        var configPath = Path.Combine(overlay, "monorepo.json");
        var csprojPath = Path.Combine(backend, "consumer", "src", "C.csproj");
        var original   = File.ReadAllText(csprojPath);

        Assert.Equal(0, await Program.Main(
            ["cpm", "--config", configPath, "--dry-run"]));

        Assert.False(File.Exists(Path.Combine(backend, "Directory.Packages.props")));
        Assert.Equal(original, File.ReadAllText(csprojPath));
    }

    [Fact]
    public async Task Cpm_picks_highest_version_on_conflict()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);

        var repoA = fx.CreateRepo("backend/a");
        var repoB = fx.CreateRepo("backend/b");
        fx.WriteCsproj(repoA, "src/A.csproj",
            "<Project><ItemGroup><PackageReference Include=\"Shared\" Version=\"1.0.0\" /></ItemGroup></Project>");
        fx.WriteCsproj(repoB, "src/B.csproj",
            "<Project><ItemGroup><PackageReference Include=\"Shared\" Version=\"2.5.0\" /></ItemGroup></Project>");

        Assert.Equal(0, await Program.Main(
            ["init", "--backend", backend, "--overlay", overlay]));
        Assert.Equal(0, await Program.Main(
            ["cpm", "--config", Path.Combine(overlay, "monorepo.json")]));

        var cpmContent = File.ReadAllText(Path.Combine(backend, "Directory.Packages.props"));
        Assert.Contains("Version=\"2.5.0\"", cpmContent);
        Assert.DoesNotContain("Version=\"1.0.0\"", cpmContent);
    }
}
