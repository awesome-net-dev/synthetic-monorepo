using Monorepo.Tool.Generation;
using Xunit;

namespace Monorepo.Tool.Tests.Generation;

public class ShimWriterTests
{
    [Fact]
    public void Shim_imports_a_path_that_is_relative_to_the_backend()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "some", "other", "place", "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);

        ShimWriter.Write(backend, overlay);

        var propsText = File.ReadAllText(Path.Combine(backend, "Directory.Build.props"));
        Assert.Contains("../some/other/place/overlay/Directory.Build.props", propsText.Replace('\\', '/'));
        Assert.DoesNotContain("synthetic-monorepo/overlay", propsText);
    }
}
