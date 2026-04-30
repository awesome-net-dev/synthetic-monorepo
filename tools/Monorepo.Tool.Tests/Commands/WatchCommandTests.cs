using Monorepo.Tool.Commands;
using Monorepo.Tool.IO;
using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;
using Xunit;

namespace Monorepo.Tool.Tests.Commands;

public class WatchCommandTests
{
    [Fact]
    public async Task Watch_returns_ConfigNotFound_when_config_path_missing()
    {
        using var fx = new TempRepoFixture();
        var missingConfig = Path.Combine(fx.Root, "nonexistent", "monorepo.json");
        Assert.Equal((int)ExitCode.ConfigNotFound, await Program.Run(
            ["watch", "--config", missingConfig],
            watcherFactory: _ => new NoOpFileWatcher()));
    }

    [Fact]
    public async Task Watch_triggers_refresh_when_csproj_changes()
    {
        using var fx = new TempRepoFixture();
        var backend = Path.Combine(fx.Root, "backend");
        var overlay = Path.Combine(fx.Root, "overlay");
        Directory.CreateDirectory(backend);
        Directory.CreateDirectory(overlay);
        var repoDir = fx.CreateRepo("backend/repo-a");
        fx.WriteCsproj(repoDir, "src/A.csproj",
            "<Project><PropertyGroup><PackageId>A.Lib</PackageId></PropertyGroup></Project>");

        Assert.Equal(0, await Program.Main(
            ["init", "--backend", backend, "--overlay", overlay]));

        var configPath = Path.Combine(overlay, "monorepo.json");
        var fakeWatcher = new ManualFileWatcher();
        var watchTask   = Task.Run(() => Program.Run(
            ["watch", "--config", configPath, "--debounce", "50"],
            watcherFactory: _ => fakeWatcher));

        // Wait until the watcher is started (deterministic — no arbitrary sleep).
        await fakeWatcher.WaitForStart().WaitAsync(TimeSpan.FromSeconds(5));

        // Add a new csproj as a producer in repo-b
        var repoB = fx.CreateRepo("backend/repo-b");
        fx.WriteCsproj(repoB, "src/B.csproj",
            "<Project><PropertyGroup><PackageId>B.Lib</PackageId></PropertyGroup></Project>");
        // Also add a consumer so the mapping is discovered
        fx.WriteCsproj(repoDir, "src/A.csproj", """
            <Project>
              <PropertyGroup><PackageId>A.Lib</PackageId></PropertyGroup>
              <ItemGroup><PackageReference Include="B.Lib" Version="1.0" /></ItemGroup>
            </Project>
            """);

        // Trigger a fake file-change event
        fakeWatcher.Trigger(Path.Combine(repoB, "src", "B.csproj"));

        // Poll for the refresh result rather than sleeping for a fixed duration.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        MonorepoConfig cfg;
        do
        {
            await Task.Delay(50);
            cfg = ConfigSerializer.Load(configPath);
        }
        while (!cfg.Mappings.Any(m => m.PackageId == "B.Lib") && DateTime.UtcNow < deadline);

        // Cancel the watch loop
        fakeWatcher.Cancel();
        await watchTask;

        Assert.Contains(cfg.Mappings, m => m.PackageId == "B.Lib");
    }
}

/// <summary>A watcher that never fires — used to test error paths.</summary>
internal sealed class NoOpFileWatcher : IFileWatcher
{
    public event Action<string>? FileChanged;
    public void Start() { _ = FileChanged; }  // suppress CS0067
    public void Dispose() { }
}

/// <summary>A watcher with manually triggered events and a cancellation signal.</summary>
internal sealed class ManualFileWatcher : IFileWatcher, IHasCancellation
{
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _started = new();
    public event Action<string>? FileChanged;
    public CancellationToken CancellationToken => _cts.Token;

    public void Start() => _started.TrySetResult();
    public Task WaitForStart() => _started.Task;
    public void Trigger(string path) => FileChanged?.Invoke(path);
    public void Cancel() => _cts.Cancel();
    public void Dispose() => _cts.Dispose();
}
