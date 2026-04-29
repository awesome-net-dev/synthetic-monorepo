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
    public string Output => Stdout.Length > 0 ? Stdout : Stderr;
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
            var tasks = repos.Select(r => RunOneAsync(r, backendRoot, args, ct));
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

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        return new RepoResult(repo.Path, stdout.TrimEnd(), stderr.TrimEnd(), proc.ExitCode);
    }
}
