using Monorepo.Tool.Discovery;
using Xunit;

namespace Monorepo.Tool.Tests.Discovery;

public class EnumerationFilterTests
{
    [Fact]
    public void MappingAnalyzer_ignores_csprojs_under_bin_and_obj()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("svc");
        fx.WriteCsproj(repo, "src/Real.csproj",
            "<Project><PropertyGroup><PackageId>Real</PackageId></PropertyGroup></Project>");
        fx.WriteCsproj(repo, "obj/Debug/Generated.csproj",
            "<Project><PropertyGroup><PackageId>Ghost</PackageId></PropertyGroup></Project>");
        fx.WriteCsproj(repo, "bin/Release/Also.csproj",
            "<Project><PropertyGroup><PackageId>AlsoGhost</PackageId></PropertyGroup></Project>");

        var result = MappingAnalyzer.Analyze(fx.Root);

        var produced = result.Repos.Single().ProducedPackages;
        Assert.Contains("Real", produced);
        Assert.DoesNotContain("Ghost", produced);
        Assert.DoesNotContain("AlsoGhost", produced);
    }
}
