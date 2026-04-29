using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class CloneCommand
{
    public static Command Build()
    {
        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Print what would be cloned without running git."
        };

        var verboseOpt = new Option<bool>("--verbose")
        {
            Description = "Show git clone output for each repo."
        };

        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walking up from CWD."
        };

        var cmd = new Command("clone")
        {
            Description = "Clone all repos listed in monorepo.json that are missing from the backend root. " +
            "Skips repos that are already present or have no URL recorded.",
            Options =
            {
                dryRunOpt,
                verboseOpt,
                configOpt
            }
        };

        cmd.SetAction(parseResult =>
        {
            var dryRun = parseResult.GetValue(dryRunOpt);
            var verbose = parseResult.GetValue(verboseOpt);
            var configFile = parseResult.GetValue(configOpt);
            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                Console.Error.WriteLine("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)IO.ExitCode.ConfigNotFound;
            }

            var config = ConfigSerializer.Load(configPath);
            var backendRoot = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(configPath)!,
                             config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));

            var cloned = 0;
            var skipped = 0;
            var missing = 0;

            foreach (var repo in config.Repos)
            {
                var targetDir = Path.GetFullPath(
                    Path.Combine(backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar)));

                if (Directory.Exists(targetDir))
                {
                    if (verbose)
                        Console.WriteLine($"  skip  {repo.Path} (already exists)");
                    skipped++;
                    continue;
                }

                if (repo.Url is null)
                {
                    Console.Error.WriteLine($"  ⚠  {repo.Path} — no URL in monorepo.json, cannot clone.");
                    missing++;
                    continue;
                }

                Console.WriteLine($"  clone {repo.Path}");
                Console.WriteLine($"        {repo.Url}");

                if (!dryRun)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetDir)!);
                    var success = RunGitClone(repo.Url, targetDir, verbose);
                    if (success)
                        cloned++;
                    else
                        missing++;
                }
                else
                {
                    cloned++;
                }
            }

            Console.WriteLine();
            if (dryRun)
                Console.WriteLine($"(dry-run) Would clone {cloned} repo(s). {skipped} already present, {missing} missing URL.");
            else
                Console.WriteLine($"Cloned {cloned} repo(s). {skipped} already present, {missing} skipped (no URL or error).");

            return missing > 0 ? (int)IO.ExitCode.GeneralError : 0;
        });

        return cmd;
    }

    private static bool RunGitClone(string url, string targetDir, bool verbose)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path.GetDirectoryName(targetDir)!,
            RedirectStandardOutput = !verbose,
            RedirectStandardError = !verbose,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("clone");
        psi.ArgumentList.Add(url);
        psi.ArgumentList.Add(targetDir);
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0 && !verbose)
            Console.Error.WriteLine($"        git clone failed (exit {proc.ExitCode}): {proc.StandardError.ReadToEnd().Trim()}");
        return proc.ExitCode == 0;
    }
}
