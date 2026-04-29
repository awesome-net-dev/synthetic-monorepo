using Monorepo.Tool.Discovery;
using Xunit;

namespace Monorepo.Tool.Tests.Discovery;

public class CsprojReaderVersionTests
{
    [Fact]
    public void ReadPackageReferencesWithVersions_returns_id_and_version()
    {
        using var fx = new TempRepoFixture();
        var csproj = fx.WriteCsproj(fx.Root, "A.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Foo.Bar" Version="2.3.1" />
                <PackageReference Include="Baz.Qux" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var refs = CsprojReader.ReadPackageReferencesWithVersions(csproj);

        Assert.Equal(2, refs.Count);
        Assert.Contains(refs, r => r.Id == "Foo.Bar" && r.Version == "2.3.1");
        Assert.Contains(refs, r => r.Id == "Baz.Qux" && r.Version == "1.0.0");
    }

    [Fact]
    public void ReadPackageReferencesWithVersions_skips_entries_with_no_version()
    {
        using var fx = new TempRepoFixture();
        var csproj = fx.WriteCsproj(fx.Root, "A.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="HasVersion" Version="1.0.0" />
                <PackageReference Include="NoVersion" />
              </ItemGroup>
            </Project>
            """);

        var refs = CsprojReader.ReadPackageReferencesWithVersions(csproj);

        Assert.Single(refs);
        Assert.Equal("HasVersion", refs[0].Id);
    }

    [Fact]
    public void ReadPackageReferencesWithVersions_skips_PrivateAssets_all()
    {
        using var fx = new TempRepoFixture();
        var csproj = fx.WriteCsproj(fx.Root, "A.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Analyzer" Version="1.0.0" PrivateAssets="all" />
                <PackageReference Include="Runtime"  Version="2.0.0" />
              </ItemGroup>
            </Project>
            """);

        var refs = CsprojReader.ReadPackageReferencesWithVersions(csproj);

        Assert.Single(refs);
        Assert.Equal("Runtime", refs[0].Id);
    }

    [Fact]
    public void ReadPackageReferencesWithVersions_is_case_insensitive_dedup()
    {
        using var fx = new TempRepoFixture();
        var csproj = fx.WriteCsproj(fx.Root, "A.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="foo.bar" Version="1.0.0" />
                <PackageReference Include="FOO.BAR" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var refs = CsprojReader.ReadPackageReferencesWithVersions(csproj);

        Assert.Single(refs);
    }
}
