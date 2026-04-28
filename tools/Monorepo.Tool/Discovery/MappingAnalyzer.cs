using Monorepo.Tool.Model;

namespace Monorepo.Tool.Discovery;

/// <summary>
/// Drives the full discovery pass:
///   1. Reads every .csproj in every repo to build a producer map (packageId → csprojPath).
///   2. Reads PackageReferences in non-exempt repos and cross-references against the producer map.
///   3. Returns candidate PackageMappings (cross-repo dependencies only).
/// </summary>
public static class MappingAnalyzer
{
    public sealed record DiscoveryResult(
        IReadOnlyList<RepoEntry>     Repos,
        IReadOnlyList<PackageMapping> Mappings,
        IReadOnlyList<string>        Warnings);

    public static DiscoveryResult Analyze(string backendRoot, bool verbose = false)
    {
        var warnings = new List<string>();
        var repos    = RepoScanner.Scan(backendRoot);
        // packageId (case-insensitive) → (csprojPath relative to backendRoot, owning repoPath)
        var producerMap = new Dictionary<string, (string CsprojPath, string RepoPath)>(
            StringComparer.OrdinalIgnoreCase);
        // Collect producer candidates before committing them so duplicates can be uncommitted atomically.
        var pendingProduced = new List<(RepoEntry Repo, string PackageId)>();
        var droppedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var repo in repos)
        {
            var repoDir = Path.Combine(backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar));
            var csprojs = FileSystemHelpers.EnumerateCsprojs(repoDir).ToArray();

            foreach (var csproj in csprojs)
            {
                string packageId;
                try
                {
                    packageId = CsprojReader.ReadPackageId(csproj);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Could not read PackageId from '{RelativeTo(csproj, backendRoot)}': {ex.Message}");
                    continue;
                }

                var csprojRel = RelativeTo(csproj, backendRoot);

                if (producerMap.TryGetValue(packageId, out var existing))
                {
                    warnings.Add(
                        $"Package '{packageId}' produced by multiple csprojs; skipping both. " +
                        $"First: '{existing.CsprojPath}', second: '{csprojRel}'.");
                    producerMap.Remove(packageId);
                    droppedPackages.Add(packageId);
                }
                else if (!droppedPackages.Contains(packageId))
                {
                    producerMap[packageId] = (csprojRel, repo.Path);
                }

                pendingProduced.Add((repo, packageId));
            }
        }

        foreach (var (repo, packageId) in pendingProduced)
        {
            if (!droppedPackages.Contains(packageId))
                repo.ProducedPackages.Add(packageId);
        }

        if (verbose)
        {
            Console.WriteLine($"  Producer map: {producerMap.Count} unique packages across {repos.Count} repos.");
        }
        var seen     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappings = new List<PackageMapping>();

        // Targets walk-up is NOT blocked by an existing Directory.Build.props —
        // it has a separate chain. Since no repo owns a Directory.Build.targets,
        // the overlay targets file reaches every csproj. Scan all repos for consumers.
        foreach (var repo in repos)
        {
            var repoDir = Path.Combine(backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar));
            var csprojs = FileSystemHelpers.EnumerateCsprojs(repoDir).ToArray();

            foreach (var csproj in csprojs)
            {
                IReadOnlyList<string> refs;
                try
                {
                    refs = CsprojReader.ReadPackageReferences(csproj);
                }
                catch (Exception ex)
                {
                    warnings.Add($"Could not read PackageReferences from '{RelativeTo(csproj, backendRoot)}': {ex.Message}");
                    continue;
                }

                foreach (var pkgId in refs)
                {
                    if (!producerMap.TryGetValue(pkgId, out var producer))
                        continue;                          // not a local package

                    if (producer.RepoPath == repo.Path)
                        continue;                          // same repo — not a cross-repo dependency

                    if (!seen.Add(pkgId))
                        continue;                          // already recorded

                    mappings.Add(new PackageMapping
                    {
                        PackageId  = pkgId,
                        CsprojPath = producer.CsprojPath,
                        Enabled    = true,
                    });

                    if (verbose)
                        Console.WriteLine($"  Mapping: {pkgId} → {producer.CsprojPath}");
                }
            }
        }

        return new DiscoveryResult(repos, mappings, warnings);
    }

    private static string RelativeTo(string fullPath, string baseDir) =>
        Path.GetRelativePath(baseDir, fullPath).Replace('\\', '/');
}
