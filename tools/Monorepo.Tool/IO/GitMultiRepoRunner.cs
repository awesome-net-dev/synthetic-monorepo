using System.Diagnostics;
using Monorepo.Tool.Model;

namespace Monorepo.Tool.IO;

public sealed record RepoResult(
    string RepoPath,
    string Stdout,
    string Stderr,
    int ExitCode)
{
    public bool Success => ExitCode == 0;
    // Concat both streams when both have content — many git commands write progress to stderr
    // and results to stdout simultaneously, so dropping either loses diagnostic information.
    public string Output => (Stdout.Length > 0 && Stderr.Length > 0)
        ? $"{Stdout}\n{Stderr}"
        : Stdout.Length > 0 ? Stdout : Stderr;
}

public static class GitMultiRepoRunner
{
    public static async Task<IReadOnlyList<RepoResult>> RunAsync(
        IEnumerable<RepoEntry> repos,
        string backendRoot,
        string[] args,
        bool parallel = false,
        CancellationToken ct = default)
    {
        if (parallel)
        {
            var semaphore = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount));
            var tasks = repos.Select(async r =>
            {
                await semaphore.WaitAsync(ct);
                try   { return await RunOneAsync(r, backendRoot, args, ct); }
                finally { semaphore.Release(); }
            });
            return await Task.WhenAll(tasks);
        }

        var results = new List<RepoResult>();
        foreach (var repo in repos)
            results.Add(await RunOneAsync(repo, backendRoot, args, ct));
        return results;
    }

    static async Task<RepoResult> RunOneAsync(
        RepoEntry repo,
        string backendRoot,
        string[] args,
        CancellationToken ct)
    {
        var repoDir = Path.Combine(
            backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar));

        if (!Directory.Exists(repoDir))
            return new RepoResult(repo.Path, "", "directory not found", -1);

        var psi = new ProcessStartInfo(args[0])
        {
            WorkingDirectory       = repoDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        foreach (var arg in args[1..])
            psi.ArgumentList.Add(arg);

        Process? proc;
        try { proc = Process.Start(psi); }
        catch (Exception ex) { return new RepoResult(repo.Path, "", ex.Message, -1); }
        if (proc is null) return new RepoResult(repo.Path, "", "could not start process", -1);

        using (proc)
        {
            try
            {
                var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
                var stderrTask = proc.StandardError.ReadToEndAsync(ct);
                await Task.WhenAll(stdoutTask, stderrTask);
                await proc.WaitForExitAsync(ct);
                return new RepoResult(repo.Path, stdoutTask.Result.TrimEnd(), stderrTask.Result.TrimEnd(), proc.ExitCode);
            }
            catch (OperationCanceledException)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                throw;
            }
        }
    }
}
