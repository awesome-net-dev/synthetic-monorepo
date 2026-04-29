using System.Xml.Linq;
using Monorepo.Tool.Discovery;
using Xunit;

namespace Monorepo.Tool.Tests.Discovery;

public class CsprojPatcherTests
{
    [Fact]
    public void StripVersionAttributes_removes_Version_from_matched_package()
    {
        using var fx = new TempRepoFixture();
        var csproj = fx.WriteCsproj(fx.Root, "A.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Foo.Bar" Version="1.2.3" />
                <PackageReference Include="Other"   Version="4.5.6" />
              </ItemGroup>
            </Project>
            """);

        CsprojPatcher.StripVersionAttributes(
            csproj,
            new HashSet<string>(["Foo.Bar"], StringComparer.OrdinalIgnoreCase),
            dryRun: false);

        var doc = XDocument.Load(csproj);
        var fooEl   = doc.Descendants("PackageReference")
            .First(e => e.Attribute("Include")?.Value == "Foo.Bar");
        var otherEl = doc.Descendants("PackageReference")
            .First(e => e.Attribute("Include")?.Value == "Other");

        Assert.Null(fooEl.Attribute("Version"));       // stripped
        Assert.NotNull(otherEl.Attribute("Version"));  // untouched
    }

    [Fact]
    public void StripVersionAttributes_is_idempotent()
    {
        using var fx = new TempRepoFixture();
        var csproj = fx.WriteCsproj(fx.Root, "A.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Foo" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);
        var ids = new HashSet<string>(["Foo"], StringComparer.OrdinalIgnoreCase);

        CsprojPatcher.StripVersionAttributes(csproj, ids, dryRun: false);
        var mtime1 = File.GetLastWriteTimeUtc(csproj);

        CsprojPatcher.StripVersionAttributes(csproj, ids, dryRun: false);
        var mtime2 = File.GetLastWriteTimeUtc(csproj);

        Assert.Equal(mtime1, mtime2);
    }

    [Fact]
    public void StripVersionAttributes_dryRun_does_not_modify_file()
    {
        using var fx = new TempRepoFixture();
        var csproj = fx.WriteCsproj(fx.Root, "A.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="Foo" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);
        var original = File.ReadAllText(csproj);

        CsprojPatcher.StripVersionAttributes(
            csproj,
            new HashSet<string>(["Foo"], StringComparer.OrdinalIgnoreCase),
            dryRun: true);

        Assert.Equal(original, File.ReadAllText(csproj));
    }

    [Fact]
    public void StripVersionAttributes_is_case_insensitive_on_package_id()
    {
        using var fx = new TempRepoFixture();
        var csproj = fx.WriteCsproj(fx.Root, "A.csproj", """
            <Project>
              <ItemGroup>
                <PackageReference Include="FOO.BAR" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        CsprojPatcher.StripVersionAttributes(
            csproj,
            new HashSet<string>(["foo.bar"], StringComparer.OrdinalIgnoreCase),
            dryRun: false);

        var doc = XDocument.Load(csproj);
        var el  = doc.Descendants("PackageReference").Single();
        Assert.Null(el.Attribute("Version"));
    }
}
