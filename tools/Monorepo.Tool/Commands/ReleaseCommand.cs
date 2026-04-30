using System.CommandLine;
using Monorepo.Tool.IO;
using Monorepo.Tool.Releases;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class ReleaseCommand
{
    public static Command Build()
    {
        var repoOpt = new Option<string?>("--repo")
        {
            Description = "Path of the target repo relative to backendRoot (e.g. 'core/common'). " +
                          "Defaults to all repos."
        };

        var bumpOpt = new Option<string>("--bump")
        {
            DefaultValueFactory = _ => "auto",
            Description = "Version bump: auto (derive from commits), major, minor, or patch."
        };

        var dryRunOpt = new Option<bool>("--dry-run")
        {
            Description = "Print release notes and next version; do not write CHANGELOG.md or create a tag."
        };

        var verboseOpt = new Option<bool>("--verbose")
        {
            Description = "Show each commit parsed."
        };

        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json."
        };
		var tagFormatOpt = new Option<string>("--tag-format")
        {
        	DefaultValueFactory = _ => "v{version}",
            Description = "Tag name template. Supports {version} and {repo} placeholders, " +
            			  "e.g. 'v{version}' (default), '{repo}/v{version}', 'release-{version}'."
        };

        var cmd = new Command("release",
            "Prepare a release for one or more managed repos: bumps version, " +
            "writes CHANGELOG.md, creates a git tag.")
        {
            repoOpt, bumpOpt, dryRunOpt, verboseOpt, tagFormatOpt, configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var repoFilter = parseResult.GetValue(repoOpt);
            var bumpArg = parseResult.GetValue(bumpOpt)!;
            var dryRun = parseResult.GetValue(dryRunOpt);
            var verbose = parseResult.GetValue(verboseOpt);
            var configFile = parseResult.GetValue(configOpt);
            var tagFormat  = parseResult.GetValue(tagFormatOpt)!;

			if (!tagFormat.Contains("{version}", StringComparison.Ordinal))
            {
                CliOutput.Error("Error: --tag-format must contain the {version} placeholder.");
                return (int)ExitCode.InvalidInput;
            }

            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                CliOutput.Error("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)ExitCode.ConfigNotFound;
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
                CliOutput.Error(repoFilter is null
                    ? "No repos found in config."
                    : $"Repo '{repoFilter}' not found in config.");
                return (int)ExitCode.InvalidInput;
            }

            foreach (var repo in targetRepos)
            {
                var repoDir = Path.Combine(backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar));
                CliOutput.Header($"\n── {repo.Path} ──────────────────────────────");

                if (!Directory.Exists(Path.Combine(repoDir, ".git"))
                    && !File.Exists(Path.Combine(repoDir, ".git")))
                {
                    CliOutput.Warning($"  ⚠  Skipping {repo.Path} — no .git found (not an initialised git repo).");
                    continue;
                }

                var repoName   = Path.GetFileName(repoDir);
                var tagGlob    = TagFormatter.ToGlob(tagFormat, repoName);
                var latestTag  = GitLogReader.GetLatestTag(repoDir, tagGlob);
                var rawCommits = GitLogReader.GetCommitsSinceTag(repoDir, latestTag);

                var parsed = rawCommits
                    .Select(c => ConventionalCommitParser.Parse(c.Subject, c.Body))
                    .Where(c => c is not null)
                    .Select(c => c!)
                    .ToList();

                if (verbose)
                    foreach (var c in rawCommits)
                        CliOutput.Muted($"  [{c.Hash[..7]}] {c.Subject}");

                var currentVersion = TagFormatter.ExtractVersion(latestTag, tagFormat, repoName) ?? "0.0.0";

                var bump = bumpArg.ToLowerInvariant() switch
                {
                    "major" => BumpType.Major,
                    "minor" => BumpType.Minor,
                    "patch" => BumpType.Patch,
                    _ => SemVerBumper.DetermineFromCommits(parsed),
                };

                var nextVersion = SemVerBumper.Bump(currentVersion, bump);

                var nextTag = TagFormatter.Resolve(tagFormat, nextVersion, repoName);
                CliOutput.Info($"  Current: {(latestTag ?? "none")}  →  Next: {nextTag}  ({bump})");
                CliOutput.Info($"  {rawCommits.Count} commit(s) since last tag ({parsed.Count} conventional)");

                PrintReleaseNotes(parsed);

                if (!dryRun)
                {
                    ChangelogWriter.Write(repoDir, nextVersion, parsed, DateTime.Today, dryRun: false);
                    GitTagger.CreateTag(repoDir, nextTag);
                    CliOutput.Success($"  ✓ Tagged {nextTag} and updated CHANGELOG.md");
                }
                else
                {
                    CliOutput.Info("  (dry-run — no changes made)");
                }
            }

            return 0;
        });

        return cmd;
    }

    private static void PrintReleaseNotes(IReadOnlyList<ConventionalCommit> commits)
    {
        if (commits.Count == 0)
        {
            CliOutput.Info("  No conventional commits to report.");
            return;
        }
        foreach (var c in commits)
        {
            var label = c.Breaking ? "BREAKING" : c.Type;
            var scope = c.Scope is not null ? $"({c.Scope})" : "";
            CliOutput.Info($"  {label}{scope}: {c.Description}");
        }
    }
}
