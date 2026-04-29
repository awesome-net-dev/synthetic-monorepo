namespace Monorepo.Tool.IO;

public static class CliOutput
{
    private static bool _enabled =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    // Injectable writers for tests — avoids competing with xunit's Console.Out redirection.
    internal static TextWriter? TestOut { get; set; }
    internal static TextWriter? TestErr { get; set; }

    public static void Success(string message) => Write(message, ConsoleColor.Green);
    public static void Warning(string message) => Write(message, ConsoleColor.Yellow);
    public static void Error(string message)   => WriteErr(message, ConsoleColor.Red);
    public static void Header(string message)  => Write(message, ConsoleColor.White);
    public static void Muted(string message)   => Write(message, ConsoleColor.DarkGray);
    public static void Info(string message)    => (TestOut ?? Console.Out).WriteLine(message);

    static void Write(string message, ConsoleColor color)
    {
        var writer = TestOut ?? Console.Out;
        if (_enabled && TestOut is null) Console.ForegroundColor = color;
        writer.WriteLine(message);
        if (_enabled && TestOut is null) Console.ResetColor();
    }

    static void WriteErr(string message, ConsoleColor color)
    {
        var writer = TestErr ?? Console.Error;
        if (_enabled && TestErr is null) Console.ForegroundColor = color;
        writer.WriteLine(message);
        if (_enabled && TestErr is null) Console.ResetColor();
    }

    public static SpinnerHandle StartSpinner(string message)
    {
        if (Console.IsOutputRedirected || !_enabled)
        {
            (TestOut ?? Console.Out).WriteLine(message);
            return SpinnerHandle.Noop;
        }
        return new SpinnerHandle(message);
    }
}
