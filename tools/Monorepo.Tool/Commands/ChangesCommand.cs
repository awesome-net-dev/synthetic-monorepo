using System.CommandLine;
using Monorepo.Tool.IO;
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
        var parallelOpt = new Option<bool>("--parallel")
        {
            Description = "Run git status in all repos concurrently."
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
            repoOpt, parallelOpt, allOpt, configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var repoFilter = parseResult.GetValue(repoOpt);
            var parallel   = parseResult.GetValue(parallelOpt);
            var showAll    = parseResult.GetValue(allOpt);
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

            var targetRepos = config.Repos
                .Where(r => repoFilter is null
                            || r.Path.Equals(repoFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (targetRepos.Count == 0)
            {
                CliOutput.Error(repoFilter is null
                    ? "No repos found in config."
                    : $"Repo '{repoFilter}' not found in config.");
                return (int)ExitCode.InvalidInput;
            }

            var results = GitMultiRepoRunner
                .RunAsync(targetRepos, backendRoot, ["git", "status", "--short"], parallel)
                .GetAwaiter().GetResult();

            var dirty = 0;
            foreach (var r in results)
            {
                if (r.ExitCode == -1)
                {
                    CliOutput.Warning($"  ⚠  {r.RepoPath} — directory not found.");
                    continue;
                }
                if (r.Output.Length == 0)
                {
                    if (showAll) CliOutput.Muted($"  ✓  {r.RepoPath}");
                    continue;
                }
                CliOutput.Header($"\n── {r.RepoPath}");
                Console.WriteLine(r.Output);
                dirty++;
            }

            Console.WriteLine();
            if (dirty == 0)
                CliOutput.Success("All repos clean.");
            else
                CliOutput.Warning($"{dirty} repo(s) with uncommitted changes.");

            return 0;
        });

        return cmd;
    }
}
