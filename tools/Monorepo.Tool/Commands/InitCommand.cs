using System.CommandLine;
using Monorepo.Tool.Discovery;
using Monorepo.Tool.Generation;
using Monorepo.Tool.IO;
using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class InitCommand
{
    public static Command Build()
    {
        var backendOpt = new Option<DirectoryInfo>("--backend")
        {
            Description = "Path to the backend root directory (contains all leaf repos).",
            Required = true,
        };

        var overlayOpt = new Option<DirectoryInfo>("--overlay")
        {
            DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory()),
            Description = "Directory where monorepo.json and the overlay/ folder will be written. " +
                         "Defaults to the current directory.",
        };

        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Print what would be written without touching the filesystem.",
        };

        var verboseOpt = new Option<bool>("--verbose")
        {
            Description = "Log every csproj scanned.",
        };

        var forceOpt = new Option<bool>("--force")
        {
            Description = "Overwrite an existing monorepo.json. Normally 'init' refuses and tells you " +
                         "to use 'generate --refresh' so manual Enabled=false overrides are preserved.",
        };

        var cmd = new Command("init",
            "Discover repos under --backend, write monorepo.json, and generate the MSBuild overlay.")
        {
            backendOpt,
            overlayOpt,
            dryRunOpt,
            verboseOpt,
            forceOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var backend = parseResult.GetValue(backendOpt)!;
            var overlay = parseResult.GetValue(overlayOpt)!;
            var dryRun  = parseResult.GetValue(dryRunOpt);
            var verbose = parseResult.GetValue(verboseOpt);
            var force   = parseResult.GetValue(forceOpt);

            var configPath = Path.Combine(overlay.FullName, "monorepo.json");
            if (File.Exists(configPath) && !force)
            {
                CliOutput.Error(
                    $"Error: {configPath} already exists. Use 'monorepo generate --refresh' to update it " +
                    "(which preserves manual Enabled=false overrides), or pass --force to overwrite.");
                return (int)ExitCode.InvalidInput;
            }

            CliOutput.Header("Discovering repos...");
            var result = MappingAnalyzer.Analyze(backend.FullName, verbose);

            // Print repo summary
            var totalRepos  = result.Repos.Count;
            var exemptRepos = result.Repos.Count(r => r.Exempt);
            CliOutput.Info($"  {totalRepos} repos found, {exemptRepos} exempt, " +
                          $"{totalRepos - exemptRepos} active.");

            foreach (var r in result.Repos.Where(r => r.Exempt))
                CliOutput.Warning($"  ⚠  EXEMPT: {r.Path} — {r.ExemptReason}");

            CliOutput.Info($"  {result.Mappings.Count} cross-repo mappings discovered.");

            foreach (var w in result.Warnings)
                CliOutput.Warning($"  ⚠  {w}");

            // Build config
            var config = new MonorepoConfig
            {
                BackendRoot = Path.GetRelativePath(overlay.FullName, backend.FullName)
                                  .Replace('\\', '/'),
                Repos    = [.. result.Repos],
                Mappings = [.. result.Mappings],
            };

            // Write monorepo.json
            if (dryRun)
                CliOutput.Muted($"  [dry-run] Would write: {configPath}");
            else
            {
                ConfigSerializer.Save(config, configPath);
                CliOutput.Muted($"  Written:   {configPath}");
            }

            // Generate overlay and solution
            var overlayDir = Path.Combine(overlay.FullName, "overlay");
            var slnxPath    = Path.Combine(overlay.FullName, "Monorepo.slnx");
            CliOutput.Header("Generating overlay files...");
            ShimWriter.Write(backend.FullName, overlayDir, dryRun);
            OverlayWriter.Write(overlayDir, backend.FullName, config.Mappings, dryRun);
            SlnxWriter.Write(slnxPath, backend.FullName, config.Repos, dryRun);

            // Activate sentinel
            CliOutput.Header("Activating sentinel...");
            SentinelManager.Activate(backend.FullName, dryRun);

            Console.WriteLine();
            CliOutput.Success("Done. Run 'monorepo status' to verify.");
            return 0;
        });

        return cmd;
    }
}
