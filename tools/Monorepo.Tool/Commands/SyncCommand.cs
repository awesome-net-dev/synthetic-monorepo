using System.CommandLine;
using Monorepo.Tool.IO;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class SyncCommand
{
    public static Command Build()
    {
        var parallelOpt = new Option<bool>("--parallel")
        {
            Description = "Pull all repos concurrently before refreshing."
        };
        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Show what would change without writing files or pulling."
        };
        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walking up from CWD."
        };

        var cmd = new Command("sync",
            "Pull all repos and refresh the overlay — the canonical start-of-day command.")
        {
            parallelOpt, dryRunOpt, configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var parallel   = parseResult.GetValue(parallelOpt);
            var dryRun     = parseResult.GetValue(dryRunOpt);
            var configFile = parseResult.GetValue(configOpt);
            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null || !File.Exists(configPath))
            {
                CliOutput.Error("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)ExitCode.ConfigNotFound;
            }

            var config = ConfigSerializer.Load(configPath);
            var backendRoot = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(configPath)!,
                    config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));

            // Phase 1: pull
            CliOutput.Header("Pulling all repos...");
            var pullResults = GitMultiRepoRunner
                .RunAsync(config.Repos, backendRoot, ["git", "pull"], parallel)
                .GetAwaiter().GetResult();

            var pulled = 0;
            var skipped = 0;
            var pullFailed = 0;
            var upToDate = 0;

            foreach (var r in pullResults)
            {
                if (r.ExitCode == -1)
                {
                    CliOutput.Warning($"  ⚠  {r.RepoPath}  {r.Stderr}");
                    skipped++;
                }
                else if (!r.Success)
                {
                    CliOutput.Error($"  ✗ {r.RepoPath}  {r.Stderr}");
                    pullFailed++;
                }
                else if (r.Stdout.Contains("Already up to date", StringComparison.OrdinalIgnoreCase))
                {
                    CliOutput.Muted($"  ✓ {r.RepoPath}  Already up to date.");
                    upToDate++;
                }
                else
                {
                    CliOutput.Success($"  ✓ {r.RepoPath}  {r.Stdout.Split('\n')[0]}");
                    pulled++;
                }
            }

            Console.WriteLine();

            // Phase 2: refresh overlay
            CliOutput.Header("Refreshing overlay...");
            GenerateCommand.RefreshResult refresh;
            try
            {
                refresh = GenerateCommand.RunRefresh(configPath, dryRun);
            }
            catch (Exception ex)
            {
                CliOutput.Error($"  Overlay refresh failed: {ex.Message}");
                return (int)ExitCode.GeneralError;
            }

            foreach (var w in refresh.Warnings)
                CliOutput.Warning($"  ⚠  {w}");

            if (refresh.Added > 0)
                CliOutput.Success($"  + {refresh.Added} new mapping(s).");
            CliOutput.Muted($"  Overlay up to date. {refresh.Total} mappings active.");

            Console.WriteLine();
            CliOutput.Info($"Done. {pulled} repo(s) updated, {upToDate} already up to date, {skipped} skipped." +
                (pullFailed > 0 ? $" {pullFailed} failed." : ""));

            return pullFailed > 0 ? (int)ExitCode.GeneralError : 0;
        });

        return cmd;
    }
}
