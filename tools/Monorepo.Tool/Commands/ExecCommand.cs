using System.CommandLine;
using System.Diagnostics;
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
            commandArg, repoOpt, allOpt, configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var args = parseResult.GetValue(commandArg)!;
            var repoFilter = parseResult.GetValue(repoOpt);
            var showAll = parseResult.GetValue(allOpt);
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

            var failed = 0;

            foreach (var repo in targetRepos)
            {
                var repoDir = Path.Combine(backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar));

                if (!Directory.Exists(repoDir))
                {
                    Console.Error.WriteLine($"  ⚠  {repo.Path} — directory not found, skipping.");
                    failed++;
                    continue;
                }

                var psi = new ProcessStartInfo(args[0])
                {
                    WorkingDirectory = repoDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                foreach (var arg in args[1..])
                    psi.ArgumentList.Add(arg);

                using var proc = Process.Start(psi)!;
                var stdout = proc.StandardOutput.ReadToEnd().TrimEnd();
                var stderr = proc.StandardError.ReadToEnd().TrimEnd();
                proc.WaitForExit();

                var output = stdout.Length > 0 && stderr.Length > 0
                    ? $"{stdout}\n{stderr}"
                    : stdout.Length > 0 ? stdout : stderr;

                if (output.Length == 0 && !showAll)
                {
                    if (proc.ExitCode != 0) failed++;
                    continue;
                }

                Console.WriteLine($"\n── {repo.Path}");
                if (output.Length > 0)
                    Console.WriteLine(output);

                if (proc.ExitCode != 0)
                {
                    Console.Error.WriteLine($"  exited with code {proc.ExitCode}");
                    failed++;
                }
            }

            Console.WriteLine();
            return failed > 0 ? (int)IO.ExitCode.GeneralError : 0;
        });

        return cmd;
    }
}
