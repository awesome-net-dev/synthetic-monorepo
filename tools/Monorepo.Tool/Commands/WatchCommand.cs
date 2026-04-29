using System.CommandLine;
using Monorepo.Tool.IO;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class WatchCommand
{
    internal sealed class RealFileWatcher : IFileWatcher
    {
        private readonly FileSystemWatcher _fsw;
        public event Action<string>? FileChanged;

        internal RealFileWatcher(string backendRoot)
        {
            _fsw = new FileSystemWatcher(backendRoot, "*.csproj")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            };
            _fsw.Changed += (_, e) => FileChanged?.Invoke(e.FullPath);
            _fsw.Created += (_, e) => FileChanged?.Invoke(e.FullPath);
            _fsw.Deleted += (_, e) => FileChanged?.Invoke(e.FullPath);
            _fsw.Renamed += (_, e) => FileChanged?.Invoke(e.FullPath);
        }

        public void Start() => _fsw.EnableRaisingEvents = true;
        public void Dispose() => _fsw.Dispose();
    }

    public static Command Build(Func<string, IFileWatcher>? watcherFactory = null)
    {
        var debounceOpt = new Option<int>("--debounce")
        {
            Description = "Milliseconds to wait after the last change before refreshing (default: 1500).",
            DefaultValueFactory = _ => 1500,
        };
        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Log what would refresh without writing files.",
        };
        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walking up from CWD.",
        };

        var cmd = new Command("watch",
            "Watch backend/ for csproj changes and auto-refresh the overlay. Runs until Ctrl+C.")
        {
            debounceOpt, dryRunOpt, configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var debounceMs = parseResult.GetValue(debounceOpt);
            var dryRun     = parseResult.GetValue(dryRunOpt);
            var configFile = parseResult.GetValue(configOpt);
            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null || !File.Exists(configPath))
            {
                CliOutput.Error("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)ExitCode.ConfigNotFound;
            }

            var config = ConfigSerializer.Load(configPath);
            var backendRoot = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(configPath)!,
                    config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));

            var factory = watcherFactory ?? (root => new RealFileWatcher(root));
            return RunWatch(configPath, backendRoot, debounceMs, dryRun, factory)
                .GetAwaiter().GetResult();
        });

        return cmd;
    }

    static async Task<int> RunWatch(
        string configPath,
        string backendRoot,
        int debounceMs,
        bool dryRun,
        Func<string, IFileWatcher> watcherFactory)
    {
        var appCts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            appCts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        using var watcher = watcherFactory(backendRoot);

        // Support test watchers that expose a CancellationToken to self-cancel the loop
        if (watcher is IHasCancellation hc)
            hc.CancellationToken.Register(() => appCts.Cancel());

        CancellationTokenSource? debounceCts = null;
        var debounceLock = new object();
        Task? lastRefreshTask = null;

        watcher.FileChanged += changedPath =>
        {
            lock (debounceLock)
            {
                // Cancel the previous debounce timer but do NOT dispose here — the linked token
                // may still be observed by the continuation that was scheduled asynchronously.
                // Ownership transfers to the cleanup block after the main loop exits.
                debounceCts?.Cancel();
                var linked = CancellationTokenSource.CreateLinkedTokenSource(appCts.Token);
                debounceCts = linked;
                var token = linked.Token;

                lastRefreshTask = Task.Delay(debounceMs, token).ContinueWith(_ =>
                {
                    if (token.IsCancellationRequested) return;
                    var rel = Path.GetRelativePath(backendRoot, changedPath);
                    CliOutput.Info($"\n[{DateTime.Now:HH:mm:ss}] Changed: {rel}");
                    CliOutput.Muted("           Refreshing overlay...");
                    try
                    {
                        var result = GenerateCommand.RunRefresh(configPath, dryRun);
                        if (result.Added > 0)
                            CliOutput.Success($"           + {result.Added} new mapping(s).");
                        CliOutput.Muted($"           Overlay up to date. {result.Total} mappings active.");
                    }
                    catch (Exception ex)
                    {
                        CliOutput.Error($"           Refresh failed: {ex.Message}");
                    }
                }, token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            }
        };

        watcher.Start();
        CliOutput.Header($"Watching {backendRoot} for csproj changes. Press Ctrl+C to stop.");

        try { await Task.Delay(Timeout.Infinite, appCts.Token); }
        catch (OperationCanceledException) { }

        Console.CancelKeyPress -= cancelHandler;

        // Await any in-flight refresh that passed the IsCancellationRequested check before
        // the token was cancelled, so it completes before we dispose the watcher.
        Task? pending;
        lock (debounceLock) { pending = lastRefreshTask; }
        if (pending is not null)
        {
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch { }
        }

        // Dispose the live debounce CTS now that no continuation can observe it.
        lock (debounceLock)
        {
            debounceCts?.Cancel();
            debounceCts?.Dispose();
            debounceCts = null;
        }

        Console.WriteLine();
        CliOutput.Muted("Stopped.");
        return 0;
    }
}

/// <summary>Optional interface for test watchers that can self-cancel the watch loop.</summary>
internal interface IHasCancellation
{
    CancellationToken CancellationToken { get; }
}
