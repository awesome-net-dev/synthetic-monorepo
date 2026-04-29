using Monorepo.Tool.IO;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class ProgramErrorTests
{
    [Fact]
    public async Task Missing_monorepo_json_returns_ConfigNotFound_exit_code()
    {
        using var fx = new TempRepoFixture();
        var cwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(fx.Root);
            var exit = await Program.Main(["status"]);
            Assert.Equal((int)ExitCode.ConfigNotFound, exit);
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
        }
    }

    [Fact]
    public async Task Unknown_command_returns_nonzero()
    {
        var exit = await Program.Main(["this-is-not-a-command"]);
        Assert.NotEqual(0, exit);
    }

    [Fact]
    public async Task Version_flag_prints_something_and_exits_zero()
    {
        var sw = new StringWriter();
        var prev = Console.Out;
        Console.SetOut(sw);
        try
        {
            var exit = await Program.Main(["--version"]);
            Assert.Equal(0, exit);
            Assert.False(string.IsNullOrWhiteSpace(sw.ToString()));
        }
        finally
        {
            Console.SetOut(prev);
        }
    }
}
