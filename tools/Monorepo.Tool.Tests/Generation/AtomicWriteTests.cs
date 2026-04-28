using Monorepo.Tool.IO;
using Xunit;

namespace Monorepo.Tool.Tests.Generation;

public class AtomicWriteTests
{
    [Fact]
    public void WriteAllText_creates_file_with_exact_content()
    {
        using var fx = new TempRepoFixture();
        var target = Path.Combine(fx.Root, "out.txt");

        AtomicFile.WriteAllText(target, "hello");

        Assert.Equal("hello", File.ReadAllText(target));
    }

    [Fact]
    public void WriteAllText_overwrites_existing_file()
    {
        using var fx = new TempRepoFixture();
        var target = Path.Combine(fx.Root, "out.txt");
        File.WriteAllText(target, "stale");

        AtomicFile.WriteAllText(target, "fresh");

        Assert.Equal("fresh", File.ReadAllText(target));
    }

    [Fact]
    public void WriteAllText_does_not_leave_temp_file_behind_on_success()
    {
        using var fx = new TempRepoFixture();
        var target = Path.Combine(fx.Root, "out.txt");

        AtomicFile.WriteAllText(target, "x");

        var leftover = Directory.EnumerateFiles(fx.Root, "*.tmp").ToArray();
        Assert.Empty(leftover);
    }
}
