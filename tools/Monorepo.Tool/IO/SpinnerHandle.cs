namespace Monorepo.Tool.IO;

public sealed class SpinnerHandle : IDisposable
{
    public static readonly SpinnerHandle Noop = new(active: false);

    private readonly bool _active;
    private readonly CancellationTokenSource? _cts;
    private readonly Task? _spinTask;
    private int _disposed;

    private SpinnerHandle(bool active) => _active = active;

    internal SpinnerHandle(string message) : this(active: true)
    {
        _cts = new CancellationTokenSource();
        Console.Write(message + " ");
        var token = _cts.Token;
        _spinTask = Task.Run(async () =>
        {
            char[] frames = ['|', '/', '-', '\\'];
            var i = 0;
            while (!token.IsCancellationRequested)
            {
                Console.Write('\b');
                i = (i + 1) % frames.Length;
                Console.Write(frames[i]);
                try   { await Task.Delay(80, token); }
                catch (OperationCanceledException) { break; }
            }
            Console.Write('\b');
            Console.Write(' ');
            Console.Write('\b');
        });
    }

    public void Dispose()
    {
        if (!_active || _cts is null) return;
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _cts.Cancel();
        try { _spinTask?.Wait(); } catch { }
        _cts.Dispose();
        Console.WriteLine();
    }
}
