using System.Diagnostics;

namespace Monorepo.Tool.Releases;

public static class GitTagger
{
    public static void CreateTag(string repoPath, string tag)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory       = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        psi.ArgumentList.Add("tag");
        psi.ArgumentList.Add("-a");
        psi.ArgumentList.Add(tag);
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add($"Release {tag}");
        using var proc = Process.Start(psi)!;
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            Console.Error.WriteLine($"  Warning: git tag failed — {stderr.Trim()}");
    }
}
