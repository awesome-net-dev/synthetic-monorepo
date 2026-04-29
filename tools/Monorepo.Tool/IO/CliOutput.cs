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

    public static void Success(string message) => Write(message, ConsoleColor.Green);
    public static void Warning(string message) => Write(message, ConsoleColor.Yellow);
    public static void Error(string message)   => WriteErr(message, ConsoleColor.Red);
    public static void Header(string message)  => Write(message, ConsoleColor.White);
    public static void Muted(string message)   => Write(message, ConsoleColor.DarkGray);
    public static void Info(string message)    => Console.WriteLine(message);

    static void Write(string message, ConsoleColor color)
    {
        if (_enabled) Console.ForegroundColor = color;
        Console.WriteLine(message);
        if (_enabled) Console.ResetColor();
    }

    static void WriteErr(string message, ConsoleColor color)
    {
        if (_enabled) Console.ForegroundColor = color;
        Console.Error.WriteLine(message);
        if (_enabled) Console.ResetColor();
    }

    public static SpinnerHandle StartSpinner(string message)
    {
        if (Console.IsOutputRedirected || !_enabled)
        {
            Console.WriteLine(message);
            return SpinnerHandle.Noop;
        }
        return new SpinnerHandle(message);
    }
}
