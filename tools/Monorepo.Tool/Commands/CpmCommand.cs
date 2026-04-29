using System.CommandLine;
using Monorepo.Tool.Discovery;
using Monorepo.Tool.Generation;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class CpmCommand
{
    public static Command Build()
    {
        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Print what would change without modifying any file."
        };

        var verboseOpt = new Option<bool>("--verbose")
        {
            Description = "Log each package version found per csproj."
        };

        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walking up from CWD."
        };

        var cmd = new Command("cpm")
        {
            Description = "Consolidate PackageReference versions into Directory.Packages.props " +
                           "(Central Package Management). Strips Version attributes from all affected csprojs. " +
                           "One-way migration — Directory.Packages.props applies unconditionally, independent of the monorepo sentinel.",
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

            Console.WriteLine("Scanning PackageReference versions...");

            var allRefs = new Dictionary<string, List<(string Version, string Csproj)>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var repo in config.Repos.Where(r => !r.Exempt))
            {
                var repoDir = Path.Combine(backendRoot,
                    repo.Path.Replace('/', Path.DirectorySeparatorChar));

                foreach (var csproj in FileSystemHelpers.EnumerateCsprojs(repoDir))
                {
                    foreach (var (id, version) in CsprojReader.ReadPackageReferencesWithVersions(csproj))
                    {
                        if (!allRefs.ContainsKey(id))
                            allRefs[id] = [];
                        allRefs[id].Add((version, csproj));

                        if (verbose)
                            Console.WriteLine($"  {id} {version} <- {csproj}");
                    }
                }
            }

            if (allRefs.Count == 0)
            {
                Console.WriteLine("No versioned PackageReferences found. Nothing to do.");
                return 0;
            }

            var consolidated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (id, entries) in allRefs)
            {
                var distinctVersions = entries.Select(e => e.Version)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var winner = distinctVersions.Aggregate((a, b) => IsHigher(b, a) ? b : a);
                consolidated[id] = winner;

                if (distinctVersions.Count > 1)
                    Console.Error.WriteLine(
                        $"  Version conflict for '{id}': {string.Join(", ", distinctVersions)} — using {winner}");
            }

            Console.WriteLine($"  {consolidated.Count} packages to centralise.");

            CpmWriter.Write(backendRoot, consolidated, dryRun);

            var packageIds = consolidated.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var processed = 0;

            foreach (var repo in config.Repos.Where(r => !r.Exempt))
            {
                var repoDir = Path.Combine(backendRoot,
                    repo.Path.Replace('/', Path.DirectorySeparatorChar));

                foreach (var csproj in FileSystemHelpers.EnumerateCsprojs(repoDir))
                {
                    CsprojPatcher.StripVersionAttributes(csproj, packageIds, dryRun);
                    processed++;
                }
            }

            Console.WriteLine($"Done. {processed} csproj(s) processed.");
            if (dryRun)
                Console.WriteLine("(dry-run — no files were changed)");

            return 0;
        });

        return cmd;
    }

    private static bool IsHigher(string candidate, string current)
    {
        var (cParts, cPre) = SplitVersion(candidate);
        var (eParts, ePre) = SplitVersion(current);
        int len = Math.Max(cParts.Length, eParts.Length);
        for (int i = 0; i < len; i++)
        {
            int a = i < cParts.Length ? cParts[i] : 0;
            int b = i < eParts.Length ? eParts[i] : 0;
            if (a != b) return a > b;
        }
        return ePre != null && cPre == null;
    }

    private static (int[] Parts, string? PreRelease) SplitVersion(string v)
    {
        var dash = v.IndexOf('-');
        var numeric = dash >= 0 ? v[..dash] : v;
        var pre = dash >= 0 ? v[(dash + 1)..] : (string?)null;
        var parts = numeric.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        return (parts, pre);
    }
}
