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

            if (configPath is null)
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
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            appCts.Cancel();
        };

        using var watcher = watcherFactory(backendRoot);

        // Support test watchers that expose a CancellationToken to self-cancel the loop
        if (watcher is IHasCancellation hc)
            hc.CancellationToken.Register(() => appCts.Cancel());

        CancellationTokenSource? debounceCts = null;
        var debounceLock = new object();

        watcher.FileChanged += changedPath =>
        {
            lock (debounceLock)
            {
                debounceCts?.Cancel();
                debounceCts?.Dispose();
                var linked = CancellationTokenSource.CreateLinkedTokenSource(appCts.Token);
                debounceCts = linked;
                var token = linked.Token;

                Task.Delay(debounceMs, token).ContinueWith(_ =>
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
