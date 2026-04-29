using System.CommandLine;
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

        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walking up from CWD."
        };

        var cmd = new Command("clean",
            "Delete bin/ and obj/ build artifact directories from all repos " +
            "and the overlay directory.")
        {
            dryRunOpt,
            verboseOpt,
            configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var dryRun     = parseResult.GetValue(dryRunOpt);
            var verbose    = parseResult.GetValue(verboseOpt);
            var configFile = parseResult.GetValue(configOpt);
            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                Console.Error.WriteLine("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)IO.ExitCode.ConfigNotFound;
            }
            
            var config = ConfigSerializer.Load(configPath);
            var configDir = Path.GetDirectoryName(configPath)!;
            var backendRoot = Path.GetFullPath(
                Path.Combine(configDir, config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));

            var roots = new List<string>();

            foreach (var repo in config.Repos)
            {
                var repoDir = Path.GetFullPath(
                    Path.Combine(backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (Directory.Exists(repoDir))
                    roots.Add(repoDir);
            }

            // Include the overlay directory (where this tool lives)
            roots.Add(configDir);

            var deleted = 0;

            foreach (var root in roots)
            {
                foreach (var dir in FindArtifactDirs(root))
                {
                    if (verbose || dryRun)
                        Console.WriteLine($"  {(dryRun ? "[dry-run] " : "")}Delete: {dir}");

                    if (!dryRun)
                    {
                        Directory.Delete(dir, recursive: true);
                        deleted++;
                    }
                    else
                    {
                        deleted++;
                    }
                }
            }

            if (deleted == 0)
                Console.WriteLine("No bin/ or obj/ directories found.");
            else if (dryRun)
                Console.WriteLine($"(dry-run) Would delete {deleted} director{(deleted == 1 ? "y" : "ies")}.");
            else
                Console.WriteLine($"Deleted {deleted} director{(deleted == 1 ? "y" : "ies")}.");

            return 0;
        });

        return cmd;
    }

    private static IEnumerable<string> FindArtifactDirs(string root)
    {
        if (!Directory.Exists(root))
            yield break;

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
