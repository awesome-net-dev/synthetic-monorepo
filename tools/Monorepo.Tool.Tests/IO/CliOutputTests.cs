using Monorepo.Tool.IO;
using Xunit;

namespace Monorepo.Tool.Tests.IO;

public class CliOutputTests
{
    [Fact]
    public void Success_writes_to_stdout()
    {
        var writer = new StringWriter();
        var prev = Console.Out;
        Console.SetOut(writer);
        try   { CliOutput.Success("hello green"); }
        finally { Console.SetOut(prev); }
        Assert.Contains("hello green", writer.ToString());
    }

    [Fact]
    public void Error_writes_to_stderr()
    {
        var writer = new StringWriter();
        var prev = Console.Error;
        Console.SetError(writer);
        try   { CliOutput.Error("bad thing"); }
        finally { Console.SetError(prev); }
        Assert.Contains("bad thing", writer.ToString());
    }

    [Fact]
    public void Info_writes_plain_to_stdout()
    {
        var writer = new StringWriter();
        var prev = Console.Out;
        Console.SetOut(writer);
        try   { CliOutput.Info("plain line"); }
        finally { Console.SetOut(prev); }
        Assert.Contains("plain line", writer.ToString());
    }

    [Fact]
    public void StartSpinner_returns_Noop_in_test_runner()
    {
        // Console.IsOutputRedirected is true in xunit — Noop is returned, no thread spawned
        using var spinner = CliOutput.StartSpinner("working");
        Assert.Same(SpinnerHandle.Noop, spinner);
    }

    [Fact]
    public void Noop_spinner_disposes_without_throwing()
    {
        SpinnerHandle.Noop.Dispose(); // safe to call multiple times
        SpinnerHandle.Noop.Dispose();
    }
}
