using Monorepo.Tool.Discovery;
using Xunit;

namespace Monorepo.Tool.Tests.Discovery;

public class MappingAnalyzerTests
{
    [Fact]
    public void Cross_repo_mapping_is_discovered_once()
    {
        using var fx = new TempRepoFixture();
        var producer = fx.CreateRepo("producer");
        var consumer = fx.CreateRepo("consumer");
        fx.WriteCsproj(producer, "src/P.csproj",
            "<Project><PropertyGroup><PackageId>P.Pkg</PackageId></PropertyGroup></Project>");
        fx.WriteCsproj(consumer, "src/C.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="P.Pkg" Version="1.0" />
              </ItemGroup>
            </Project>
            """);

        var result = MappingAnalyzer.Analyze(fx.Root);

        var mapping = Assert.Single(result.Mappings);
        Assert.Equal("P.Pkg", mapping.PackageId);
        Assert.EndsWith("P.csproj", mapping.CsprojPath);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Duplicate_producer_is_dropped_from_both_ProducedPackages_lists()
    {
        using var fx = new TempRepoFixture();
        var first  = fx.CreateRepo("a");
        var second = fx.CreateRepo("b");
        fx.WriteCsproj(first,  "A.csproj", "<Project><PropertyGroup><PackageId>Dup</PackageId></PropertyGroup></Project>");
        fx.WriteCsproj(second, "B.csproj", "<Project><PropertyGroup><PackageId>Dup</PackageId></PropertyGroup></Project>");

        var result = MappingAnalyzer.Analyze(fx.Root);

        foreach (var repo in result.Repos)
            Assert.DoesNotContain("Dup", repo.ProducedPackages);
        Assert.Single(result.Warnings);
        Assert.Contains("Dup", result.Warnings[0]);
    }

    [Fact]
    public void Same_repo_dependencies_are_not_reported_as_cross_repo_mappings()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("solo");
        fx.WriteCsproj(repo, "lib/Lib.csproj",
            "<Project><PropertyGroup><PackageId>Lib</PackageId></PropertyGroup></Project>");
        fx.WriteCsproj(repo, "app/App.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Lib" Version="1.0" />
              </ItemGroup>
            </Project>
            """);

        var result = MappingAnalyzer.Analyze(fx.Root);

        Assert.Empty(result.Mappings);
    }
}
