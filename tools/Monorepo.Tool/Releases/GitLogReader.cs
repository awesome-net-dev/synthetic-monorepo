using System.Diagnostics;

namespace Monorepo.Tool.Releases;

public sealed record CommitInfo(string Hash, string Subject, string? Body);

public static class GitLogReader
{
    public static string? GetLatestTag(string repoPath, string match = "v*")
    {
        var output = RunGit(repoPath, $"describe --tags --abbrev=0 --match \"{match}\"");
        return string.IsNullOrWhiteSpace(output) ? null : output.Trim();
    }

    public static IReadOnlyList<CommitInfo> GetCommitsSinceTag(string repoPath, string? sinceTag)
    {
        const string format = "%H%x1f%s%x1f%b%x1e";
        var range  = sinceTag is null ? "" : $"{sinceTag}..HEAD";
        var output = RunGit(repoPath, $"log {range} --pretty=format:\"{format}\"");

        if (string.IsNullOrWhiteSpace(output)) return [];

        return output
            .Split('\x1e', StringSplitOptions.RemoveEmptyEntries)
            .Select(raw =>
            {
                var parts   = raw.Split('\x1f');
                var hash    = parts.Length > 0 ? parts[0].Trim() : "";
                var subject = parts.Length > 1 ? parts[1].Trim() : "";
                var body    = parts.Length > 2 && parts[2].Trim().Length > 0
                                  ? parts[2].Trim() : null;
                return new CommitInfo(hash, subject, body);
            })
            .Where(c => c.Hash.Length > 0)
            .ToList();
    }

    private static string RunGit(string repoPath, string arguments)
    {
        var psi = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("git not found in PATH.");
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return proc.ExitCode == 0 ? stdout : "";
    }
}
