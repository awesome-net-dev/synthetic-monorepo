using System.CommandLine;
using Monorepo.Tool.IO;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class FetchCommand
{
    public static Command Build()
    {
        var repoOpt = new Option<string?>("--repo")
        {
            Description = "Run only in this repo (path relative to backend root)."
        };
        var parallelOpt = new Option<bool>("--parallel")
        {
            Description = "Fetch all repos concurrently."
        };
        var allOpt = new Option<bool>("--all")
        {
            Description = "Show a line for every repo even when fetch produced no output."
        };
        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walking up from CWD."
        };

        var cmd = new Command("fetch", "Run git fetch in every repo.")
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
                .RunAsync(targetRepos, backendRoot, ["git", "fetch"], parallel)
                .GetAwaiter().GetResult();

            foreach (var r in results)
            {
                if (r.ExitCode == -1)
                    CliOutput.Warning($"  ⚠  {r.RepoPath}  {r.Stderr}");
                else if (!r.Success)
                    CliOutput.Error($"  ✗ {r.RepoPath}  {r.Stderr}");
                else if (r.Output.Length > 0)
                    CliOutput.Success($"  ✓ {r.RepoPath}  {r.Output.Split('\n')[0]}");
                else if (showAll)
                    CliOutput.Muted($"  ✓ {r.RepoPath}");
            }

            var failed = results.Count(r => !r.Success && r.ExitCode != -1);
            Console.WriteLine();
            return failed > 0 ? (int)ExitCode.GeneralError : 0;
        });

        return cmd;
    }
}
