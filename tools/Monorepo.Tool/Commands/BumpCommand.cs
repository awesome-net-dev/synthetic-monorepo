using System.CommandLine;
using Monorepo.Tool.Releases;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class BumpCommand
{
    public static Command Build()
    {
        var cmd = new Command("bump",
            "Create a git tag for the next version without writing CHANGELOG.md.");

        cmd.Add(BuildVariant("patch", BumpType.Patch));
        cmd.Add(BuildVariant("minor", BumpType.Minor));
        cmd.Add(BuildVariant("major", BumpType.Major));

        return cmd;
    }

    private static Command BuildVariant(string name, BumpType bump)
    {
        var repoOpt = new Option<string?>("--repo")
        {
            Description = "Path of the target repo relative to backendRoot. Defaults to all repos."
        };

        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Print next version without creating a tag."
        };

        var verboseOpt = new Option<bool>("--verbose")
        {
            Description = "Show current and next tag for each repo."
        };

        var tagFormatOpt = new Option<string>("--tag-format")
        {
            Description = "Tag name template. Supports {version} and {repo} placeholders.",
            DefaultValueFactory = _ => "v{version}"
        };

        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json."
        };

        var sub = new Command(name, $"Bump the {name} version component and create a git tag.")
        {
            repoOpt, dryRunOpt, verboseOpt, tagFormatOpt, configOpt,
        };

        sub.SetAction(parseResult =>
        {
            var repoFilter = parseResult.GetValue(repoOpt);
            var dryRun = parseResult.GetValue(dryRunOpt);
            var verbose = parseResult.GetValue(verboseOpt);
            var tagFormat = parseResult.GetValue(tagFormatOpt)!;
            var configFile = parseResult.GetValue(configOpt);

            if (!tagFormat.Contains("{version}", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Error: --tag-format must contain the {version} placeholder.");
                return (int)IO.ExitCode.InvalidInput;
            }

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

            foreach (var repo in targetRepos)
            {
                var repoDir = Path.Combine(backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar));
                Console.WriteLine($"\n── {repo.Path} ──────────────────────────────");

                if (!Directory.Exists(Path.Combine(repoDir, ".git"))
                    && !File.Exists(Path.Combine(repoDir, ".git")))
                {
                    Console.Error.WriteLine($"  ⚠  Skipping {repo.Path} — no .git found.");
                    continue;
                }

                var repoName = Path.GetFileName(repoDir);
                var tagGlob = TagFormatter.ToGlob(tagFormat, repoName);
                var latestTag = GitLogReader.GetLatestTag(repoDir, tagGlob);
                var currentVersion = TagFormatter.ExtractVersion(latestTag, tagFormat, repoName) ?? "0.0.0";
                var nextVersion = SemVerBumper.Bump(currentVersion, bump);
                var nextTag = TagFormatter.Resolve(tagFormat, nextVersion, repoName);

                if (verbose || dryRun)
                    Console.WriteLine($"  Current: {(latestTag ?? "none")}  →  Next: {nextTag}");

                if (!dryRun)
                {
                    GitTagger.CreateTag(repoDir, nextTag);
                    Console.WriteLine($"  ✓ Tagged {nextTag}");
                }
                else
                {
                    Console.WriteLine("  (dry-run — no tag created)");
                }
            }

            return 0;
        });

        return sub;
    }
}
