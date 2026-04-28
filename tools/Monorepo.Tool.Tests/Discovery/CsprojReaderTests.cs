using Monorepo.Tool.Discovery;
using Xunit;

namespace Monorepo.Tool.Tests.Discovery;

public class CsprojReaderTests
{
    [Fact]
    public void PackageId_prefers_top_level_PackageId_over_nested_occurrences()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("repo");
        var csproj = fx.WriteCsproj(repo, "Foo.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Real.Id</PackageId>
              </PropertyGroup>
              <Target Name="Noise">
                <ItemGroup>
                  <FakeProp Include="x">
                    <PackageId>Nested.Should.Be.Ignored</PackageId>
                  </FakeProp>
                </ItemGroup>
              </Target>
            </Project>
            """);

        Assert.Equal("Real.Id", CsprojReader.ReadPackageId(csproj));
    }

    [Fact]
    public void PackageId_falls_back_to_AssemblyName_then_filename()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("repo");
        var withAsm = fx.WriteCsproj(repo, "A.csproj",
            "<Project><PropertyGroup><AssemblyName>Asm.Name</AssemblyName></PropertyGroup></Project>");
        var plain  = fx.WriteCsproj(repo, "B.csproj", "<Project/>");

        Assert.Equal("Asm.Name", CsprojReader.ReadPackageId(withAsm));
        Assert.Equal("B",        CsprojReader.ReadPackageId(plain));
    }

    [Fact]
    public void PackageReferences_excludes_PrivateAssets_all_and_dedups_case_insensitively()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("repo");
        var csproj = fx.WriteCsproj(repo, "X.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Real.Pkg" Version="1.0" />
                <PackageReference Include="REAL.PKG" Version="1.0" />
                <PackageReference Include="Analyzer.Only" PrivateAssets="all" />
                <PackageReference Include="Also.Analyzer">
                  <PrivateAssets>all</PrivateAssets>
                </PackageReference>
              </ItemGroup>
            </Project>
            """);

        var refs = CsprojReader.ReadPackageReferences(csproj);

        Assert.Single(refs);
        Assert.Equal("Real.Pkg", refs[0]);
    }

    [Fact]
    public void PackageReferences_ignores_entries_inside_Target_elements()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("repo");
        var csproj = fx.WriteCsproj(repo, "Y.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Visible" Version="1.0" />
              </ItemGroup>
              <Target Name="Noise">
                <ItemGroup>
                  <PackageReference Include="Hidden" Version="1.0" />
                </ItemGroup>
              </Target>
            </Project>
            """);

        var refs = CsprojReader.ReadPackageReferences(csproj);

        Assert.Single(refs);
        Assert.Equal("Visible", refs[0]);
    }

    [Fact]
    public void Reads_file_with_utf8_bom_without_throwing()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("repo");
        var path = Path.Combine(repo, "Z.csproj");
        var bom  = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = System.Text.Encoding.UTF8.GetBytes("<Project><PropertyGroup><PackageId>Bom.Id</PackageId></PropertyGroup></Project>");
        File.WriteAllBytes(path, [.. bom, .. body]);

        Assert.Equal("Bom.Id", CsprojReader.ReadPackageId(path));
    }
}
