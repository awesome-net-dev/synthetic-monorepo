using System.CommandLine;
using System.Diagnostics;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class ChangesCommand
{
    public static Command Build()
    {
        var repoOpt = new Option<string?>("--repo")
        {
            Description = "Path of the target repo relative to backendRoot. Defaults to all repos."
        };

        var allOpt = new Option<bool>("--all")
        {
            Description = "Also list repos with no changes (clean)."
        };

        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walking up from CWD."
        };

        var cmd = new Command("changes",
            "Show 'git status --short' for every repo. Clean repos are hidden unless --all is passed.")
        {
            repoOpt, allOpt, configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var repoFilter = parseResult.GetValue(repoOpt);
            var showAll    = parseResult.GetValue(allOpt);
            var configFile = parseResult.GetValue(configOpt);
            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                Console.Error.WriteLine("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)IO.ExitCode.ConfigNotFound;
            }

            var config      = ConfigSerializer.Load(configPath);
            var backendRoot = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(configPath)!,
                             config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));

            var targetRepos = config.Repos
                .Where(r => repoFilter is null
                            || r.Path.Equals(repoFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (targetRepos.Count == 0)
            {
                Console.Error.WriteLine(repoFilter is null
                    ? "No repos found in config."
                    : $"Repo '{repoFilter}' not found in config.");
                return (int)IO.ExitCode.InvalidInput;
            }

            var dirty = 0;

            foreach (var repo in targetRepos)
            {
                var repoDir = Path.Combine(backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar));

                if (!Directory.Exists(repoDir))
                {
                    Console.Error.WriteLine($"  ⚠  {repo.Path} — directory not found.");
                    continue;
                }

                var psi = new ProcessStartInfo("git")
                {
                    WorkingDirectory       = repoDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                };
                psi.ArgumentList.Add("status");
                psi.ArgumentList.Add("--short");

                using var proc = Process.Start(psi)!;
                var output = proc.StandardOutput.ReadToEnd().TrimEnd();
                proc.WaitForExit();

                if (output.Length == 0)
                {
                    if (showAll)
                        Console.WriteLine($"  ✓  {repo.Path}");
                    continue;
                }

                Console.WriteLine($"\n── {repo.Path}");
                Console.WriteLine(output);
                dirty++;
            }

            Console.WriteLine();
            Console.WriteLine(dirty == 0
                ? "All repos clean."
                : $"{dirty} repo(s) with uncommitted changes.");

            return 0;
        });

        return cmd;
    }
}
