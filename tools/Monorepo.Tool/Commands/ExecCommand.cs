using System.CommandLine;
using Monorepo.Tool.IO;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class ExecCommand
{
    public static Command Build()
    {
        var commandArg = new Argument<string[]>("command")
        {
            Arity = ArgumentArity.OneOrMore,
            Description = "Command and arguments to run in each repo (pass after --).",
        };
        var repoOpt = new Option<string?>("--repo")
        {
            Description = "Path of the target repo relative to backendRoot. Defaults to all repos."
        };
        var parallelOpt = new Option<bool>("--parallel")
        {
            Description = "Run the command in all repos concurrently."
        };
        var allOpt = new Option<bool>("--all")
        {
            Description = "Show a header for every repo, even those that produce no output."
        };
        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walking up from CWD."
        };

        var cmd = new Command("exec",
            "Run an arbitrary command in every repo. " +
            "Repos that produce no output are hidden unless --all is passed.")
        {
            commandArg, repoOpt, parallelOpt, allOpt, configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var args       = parseResult.GetValue(commandArg)!;
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
                .RunAsync(targetRepos, backendRoot, args, parallel)
                .GetAwaiter().GetResult();

            var failed = 0;
            foreach (var r in results)
            {
                if (r.ExitCode == -1)
                {
                    CliOutput.Warning($"  ⚠  {r.RepoPath} — directory not found, skipping.");
                    failed++;
                    continue;
                }

                if (r.Output.Length == 0 && !showAll)
                {
                    if (!r.Success) failed++;
                    continue;
                }

                CliOutput.Header($"\n── {r.RepoPath}");
                if (r.Output.Length > 0)
                    Console.WriteLine(r.Output);

                if (!r.Success)
                {
                    CliOutput.Error($"  exited with code {r.ExitCode}");
                    failed++;
                }
            }

            Console.WriteLine();
            return failed > 0 ? (int)ExitCode.GeneralError : 0;
        });

        return cmd;
    }
}
