using System.CommandLine;
using Monorepo.Tool.Discovery;
using Monorepo.Tool.Generation;
using Monorepo.Tool.IO;
using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class GenerateCommand
{
    public sealed record RefreshResult(
        int Added,
        int Stale,
        int Total,
        IReadOnlyList<string> Warnings);

    public static RefreshResult RunRefresh(
        string configPath,
        bool dryRun = false,
        bool verbose = false)
    {
        var config     = ConfigSerializer.Load(configPath);
        var configDir  = Path.GetDirectoryName(configPath)!;
        var overlayDir = Path.Combine(configDir, "overlay");
        var backendRoot = Path.GetFullPath(
            Path.Combine(configDir, config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));

        var scan = MappingAnalyzer.Analyze(backendRoot, verbose);

        var existingByPkg = config.Mappings
            .ToDictionary(m => m.PackageId, StringComparer.OrdinalIgnoreCase);

        var merged = scan.Mappings.Select(discovered =>
        {
            if (existingByPkg.TryGetValue(discovered.PackageId, out var existing))
                return new PackageMapping
                {
                    PackageId  = existing.PackageId,
                    CsprojPath = discovered.CsprojPath,
                    Enabled    = existing.Enabled,
                };
            return discovered;
        }).ToList();

        var discoveredPkgs = scan.Mappings
            .Select(m => m.PackageId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = config.Mappings
            .Where(m => !discoveredPkgs.Contains(m.PackageId))
            .Select(m => $"Stale mapping removed: '{m.PackageId}' (csproj no longer found).")
            .ToList();

        var added = scan.Mappings.Count(m => !existingByPkg.ContainsKey(m.PackageId));

        var existingReposByPath = config.Repos
            .ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);

        config.Repos = scan.Repos.Select(discovered =>
        {
            if (existingReposByPath.TryGetValue(discovered.Path, out var existing))
                discovered.Url ??= existing.Url;
            return discovered;
        }).ToList();

        config.Mappings = merged;

        if (!dryRun)
            ConfigSerializer.Save(config, configPath);

        var slnxPath = Path.Combine(configDir, "Monorepo.slnx");
        ShimWriter.Write(backendRoot, overlayDir, dryRun);
        OverlayWriter.Write(overlayDir, backendRoot, config.Mappings, dryRun);
        SlnxWriter.Write(slnxPath, backendRoot, config.Repos, dryRun);

        return new RefreshResult(
            added,
            stale.Count,
            config.Mappings.Count,
            scan.Warnings.Concat(stale).ToList());
    }

    public static Command Build()
    {
        var refreshOpt = new Option<bool>("--refresh")
        {
            Description = "Re-scan repos and update monorepo.json before regenerating the overlay.",
        };
        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Print what would be written without touching the filesystem.",
        };
        var verboseOpt = new Option<bool>("--verbose")
        {
            Description = "Log every csproj scanned (only meaningful with --refresh).",
        };
        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. " +
                          "Defaults to the first monorepo.json found by walking up from the current directory.",
        };

        var cmd = new Command("generate",
            "Regenerate the MSBuild overlay from monorepo.json (optionally re-scanning repos first).")
        {
            refreshOpt, dryRunOpt, verboseOpt, configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var refresh    = parseResult.GetValue(refreshOpt);
            var dryRun     = parseResult.GetValue(dryRunOpt);
            var verbose    = parseResult.GetValue(verboseOpt);
            var configFile = parseResult.GetValue(configOpt);

            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                CliOutput.Error("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)ExitCode.ConfigNotFound;
            }

            if (refresh)
            {
                CliOutput.Header("Re-scanning repos...");
                var result = RunRefresh(configPath, dryRun, verbose);

                foreach (var w in result.Warnings)
                    CliOutput.Warning($"  ⚠  {w}");

                CliOutput.Muted($"  {result.Total} mappings active" +
                    (result.Added > 0 ? $", {result.Added} new" : "") +
                    (result.Stale > 0 ? $", {result.Stale} stale removed" : "") + ".");

                if (dryRun) CliOutput.Muted($"  [dry-run] Would update: {configPath}");
                else        CliOutput.Info($"  Updated: {configPath}");
            }
            else
            {
                var config     = ConfigSerializer.Load(configPath);
                var configDir  = Path.GetDirectoryName(configPath)!;
                var overlayDir = Path.Combine(configDir, "overlay");
                var backendRoot = Path.GetFullPath(
                    Path.Combine(configDir,
                        config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));
                var slnxPath = Path.Combine(configDir, "Monorepo.slnx");

                CliOutput.Header("Generating overlay files...");
                ShimWriter.Write(backendRoot, overlayDir, dryRun);
                OverlayWriter.Write(overlayDir, backendRoot, config.Mappings, dryRun);
                SlnxWriter.Write(slnxPath, backendRoot, config.Repos, dryRun);
            }

            CliOutput.Success("Done.");
            return 0;
        });

        return cmd;
    }
}
