using System.CommandLine;
using Monorepo.Tool.IO;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class CleanCommand
{
    public static Command Build()
    {
        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Print what would be deleted without removing anything."
        };
        var verboseOpt = new Option<bool>("--verbose")
        {
            Description = "List every directory as it is deleted."
        };
        var parallelOpt = new Option<bool>("--parallel")
        {
            Description = "Delete artifact directories in all repos concurrently."
        };
        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walking up from CWD."
        };

        var cmd = new Command("clean",
            "Delete bin/ and obj/ build artifact directories from all repos " +
            "and the overlay directory.")
        {
            dryRunOpt, verboseOpt, parallelOpt, configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var dryRun     = parseResult.GetValue(dryRunOpt);
            var verbose    = parseResult.GetValue(verboseOpt);
            var parallel   = parseResult.GetValue(parallelOpt);
            var configFile = parseResult.GetValue(configOpt);
            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                CliOutput.Error("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)ExitCode.ConfigNotFound;
            }

            var config    = ConfigSerializer.Load(configPath);
            var configDir = Path.GetDirectoryName(configPath)!;
            var backendRoot = Path.GetFullPath(
                Path.Combine(configDir, config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));

            var roots = config.Repos
                .Select(r => Path.GetFullPath(
                    Path.Combine(backendRoot, r.Path.Replace('/', Path.DirectorySeparatorChar))))
                .Where(Directory.Exists)
                .Append(configDir)
                .ToList();

            var deleted = 0;

            void CleanRoot(string root)
            {
                foreach (var dir in FindArtifactDirs(root))
                {
                    if (verbose || dryRun)
                        CliOutput.Muted($"  {(dryRun ? "[dry-run] " : "")}Delete: {dir}");
                    if (!dryRun)
                        Directory.Delete(dir, recursive: true);
                    Interlocked.Increment(ref deleted);
                }
            }

            if (parallel)
                Parallel.ForEach(roots, CleanRoot);
            else
                roots.ForEach(CleanRoot);

            if (deleted == 0)
                CliOutput.Muted("No bin/ or obj/ directories found.");
            else if (dryRun)
                CliOutput.Muted($"(dry-run) Would delete {deleted} director{(deleted == 1 ? "y" : "ies")}.");
            else
                CliOutput.Success($"Deleted {deleted} director{(deleted == 1 ? "y" : "ies")}.");

            return 0;
        });

        return cmd;
    }

    static IEnumerable<string> FindArtifactDirs(string root)
    {
        if (!Directory.Exists(root)) yield break;
        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(dir);
            if (name is "bin" or "obj")
                yield return dir;
            else
                foreach (var nested in FindArtifactDirs(dir))
                    yield return nested;
        }
    }
}
