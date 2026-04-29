using System.Diagnostics;
using Monorepo.Tool.Model;

namespace Monorepo.Tool.Discovery;

/// <summary>
/// Finds leaf git repos under the backend root.
/// Scans depth 1 and depth 2 (e.g. "core/common" as well as "users").
/// A leaf repo is any directory that contains a .git entry (file = worktree, directory = normal clone).
/// </summary>
public static class RepoScanner
{
    public static IReadOnlyList<RepoEntry> Scan(string backendRoot)
    {
        var results = new List<RepoEntry>();
        var root = new DirectoryInfo(backendRoot);

        if (!root.Exists)
            throw new DirectoryNotFoundException($"Backend root not found: {backendRoot}");

        foreach (var depth1 in root.EnumerateDirectories())
        {
            if (ShouldSkip(depth1)) continue;
            if (IsGitRepo(depth1))
            {
                results.Add(BuildEntry(depth1, backendRoot));
            }
            else
            {
                // grouping directory — scan one level deeper
                foreach (var depth2 in depth1.EnumerateDirectories())
                {
                    if (ShouldSkip(depth2)) continue;
                    if (IsGitRepo(depth2))
                        results.Add(BuildEntry(depth2, backendRoot));
                }
            }
        }

        return results;
    }

    private static bool ShouldSkip(DirectoryInfo dir) =>
        dir.Name is "bin" or "obj" or "node_modules" or ".vs" or ".idea"
        || (dir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;

    // A directory is a git repo when it contains .git (file for worktrees, directory for clones).
    private static bool IsGitRepo(DirectoryInfo dir) =>
        Directory.Exists(Path.Combine(dir.FullName, ".git"))
        || File.Exists(Path.Combine(dir.FullName, ".git"));

    private static RepoEntry BuildEntry(DirectoryInfo repoDir, string backendRoot)
    {
        var relativePath = Path.GetRelativePath(backendRoot, repoDir.FullName)
                               .Replace('\\', '/');

        var hasOwnDbp = File.Exists(Path.Combine(repoDir.FullName, "Directory.Build.props"));

        return new RepoEntry
        {
            Path             = relativePath,
            Exempt           = hasOwnDbp,
            ExemptReason     = hasOwnDbp ? "owns Directory.Build.props" : null,
            Url              = GetRemoteUrl(repoDir.FullName),
            ProducedPackages = [],
        };
    }

    private static string? GetRemoteUrl(string repoPath)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        psi.ArgumentList.Add("remote");
        psi.ArgumentList.Add("get-url");
        psi.ArgumentList.Add("origin");
        using var proc = Process.Start(psi);
        if (proc is null) return null;
        var url = proc.StandardOutput.ReadToEnd().Trim();
        proc.WaitForExit();
        return proc.ExitCode == 0 && url.Length > 0 ? url : null;
    }
}
