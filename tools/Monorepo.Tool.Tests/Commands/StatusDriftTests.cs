using Monorepo.Tool.IO;
using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class StatusDriftTests
{
    [Fact]
    public async Task Status_returns_Drift_exit_code_when_mapped_csproj_is_missing()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);

        var configPath = Path.Combine(overlay, "monorepo.json");
        ConfigSerializer.Save(new MonorepoConfig
        {
            BackendRoot = "../backend",
            Repos    = [new RepoEntry { Path = "ghost" }],
            Mappings = [new PackageMapping
            {
                PackageId  = "Ghost.Pkg",
                CsprojPath = "ghost/does/not/exist.csproj",
                Enabled    = true,
            }],
        }, configPath);

        var exit = await Program.Main(["status", "--config", configPath]);

        Assert.Equal((int)ExitCode.Drift, exit);
    }

    [Fact]
    public async Task Status_returns_zero_when_all_mapped_csprojs_exist_on_disk()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);
        var csprojPath = Path.Combine(backend, "real", "Real.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(csprojPath)!);
        File.WriteAllText(csprojPath, "<Project/>");

        var configPath = Path.Combine(overlay, "monorepo.json");
        ConfigSerializer.Save(new MonorepoConfig
        {
            BackendRoot = "../backend",
            Repos    = [new RepoEntry { Path = "real" }],
            Mappings = [new PackageMapping
            {
                PackageId  = "Real.Pkg",
                CsprojPath = "real/Real.csproj",
                Enabled    = true,
            }],
        }, configPath);

        var exit = await Program.Main(["status", "--config", configPath]);

        Assert.Equal(0, exit);
    }
}
