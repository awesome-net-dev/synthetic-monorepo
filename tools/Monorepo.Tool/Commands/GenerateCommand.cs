using System.CommandLine;
using Monorepo.Tool.Discovery;
using Monorepo.Tool.Generation;
using Monorepo.Tool.Model;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class GenerateCommand
{
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
            refreshOpt,
            dryRunOpt,
            verboseOpt,
            configOpt,
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var refresh    = parseResult.GetValue(refreshOpt);
            var dryRun     = parseResult.GetValue(dryRunOpt);
            var verbose    = parseResult.GetValue(verboseOpt);
            var configFile = parseResult.GetValue(configOpt);

            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                Console.Error.WriteLine("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)Monorepo.Tool.IO.ExitCode.ConfigNotFound;
            }

            var config     = ConfigSerializer.Load(configPath);
            var overlayDir = Path.Combine(Path.GetDirectoryName(configPath)!, "overlay");
            var backendRoot = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(configPath)!,
                             config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));

            if (refresh)
            {
                Console.WriteLine("Re-scanning repos...");
                var result = MappingAnalyzer.Analyze(backendRoot, verbose);

                // Merge: preserve Enabled=false overrides; add new; warn on disappeared.
                var existingByPkg = config.Mappings
                    .ToDictionary(m => m.PackageId, StringComparer.OrdinalIgnoreCase);

                var merged = result.Mappings.Select(discovered =>
                {
                    if (existingByPkg.TryGetValue(discovered.PackageId, out var existing))
                        return new PackageMapping
                        {
                            PackageId  = existing.PackageId,
                            CsprojPath = discovered.CsprojPath,   // refresh path from disk
                            Enabled    = existing.Enabled,        // preserve manual override
                        };
                    return discovered;
                }).ToList();

                // Warn about mappings that disappeared from disk.
                var discoveredPkgs = result.Mappings
                    .Select(m => m.PackageId)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var stale in config.Mappings.Where(m => !discoveredPkgs.Contains(m.PackageId)))
                    Console.WriteLine($"  ⚠  Stale mapping removed: '{stale.PackageId}' (csproj no longer found).");

                config.Repos    = [.. result.Repos];
                config.Mappings = merged;

                foreach (var w in result.Warnings)
                    Console.WriteLine($"  ⚠  {w}");

                if (!dryRun)
                {
                    ConfigSerializer.Save(config, configPath);
                    Console.WriteLine($"  Updated: {configPath}");
                }
                else
                {
                    Console.WriteLine($"  [dry-run] Would update: {configPath}");
                }
            }

            var slnPath = Path.Combine(Path.GetDirectoryName(configPath)!, "Monorepo.sln");
            Console.WriteLine("Generating overlay files...");
            ShimWriter.Write(backendRoot, overlayDir, dryRun);
            OverlayWriter.Write(overlayDir, backendRoot, config.Mappings, dryRun);
            SolutionWriter.Write(slnPath, backendRoot, config.Repos, dryRun);

            Console.WriteLine("Done.");
            return 0;
        });

        return cmd;
    }
}
