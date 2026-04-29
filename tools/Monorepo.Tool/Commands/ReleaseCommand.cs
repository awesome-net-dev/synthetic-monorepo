using System.CommandLine;
using System.Diagnostics;
using Monorepo.Tool.Release;
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

        var dryRunOpt = new Option<bool>("--dry-run",
            "Print release notes and next version; do not write CHANGELOG.md or create a tag.");

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
                    Console.Error.WriteLine($"  ⚠  Skipping {repo.Path} — no .git found (not an initialised git repo).");
                    continue;
                }

                var repoName   = Path.GetFileName(repoDir);
                var tagGlob    = BuildTagGlob(tagFormat, repoName);
                var latestTag  = GitLogReader.GetLatestTag(repoDir, tagGlob);
                var rawCommits = GitLogReader.GetCommitsSinceTag(repoDir, latestTag);

                var parsed = rawCommits
                    .Select(c => ConventionalCommitParser.Parse(c.Subject, c.Body))
                    .Where(c => c is not null)
                    .Select(c => c!)
                    .ToList();

                if (verbose)
                    foreach (var c in rawCommits)
                        Console.WriteLine($"  [{c.Hash[..7]}] {c.Subject}");

                var currentVersion = ExtractVersion(latestTag, tagFormat, repoName) ?? "0.0.0";

                var bump = bumpArg.ToLowerInvariant() switch
                {
                    "major" => BumpType.Major,
                    "minor" => BumpType.Minor,
                    "patch" => BumpType.Patch,
                    _ => SemVerBumper.DetermineFromCommits(parsed),
                };

                var nextVersion = SemVerBumper.Bump(currentVersion, bump);

                var nextTag = ResolveTag(tagFormat, nextVersion, repoName);
                Console.WriteLine($"  Current: {(latestTag ?? "none")}  →  Next: {nextTag}  ({bump})");
                Console.WriteLine($"  {rawCommits.Count} commit(s) since last tag ({parsed.Count} conventional)");

                PrintReleaseNotes(parsed);

                if (!dryRun)
                {
                    ChangelogWriter.Write(repoDir, nextVersion, parsed, DateTime.Today, dryRun: false);
                    CreateGitTag(repoDir, nextTag);
                    Console.WriteLine($"  ✓ Tagged {nextTag} and updated CHANGELOG.md");
                }
                else
                {
                    Console.WriteLine("  (dry-run — no changes made)");
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
            Console.WriteLine("  No conventional commits to report.");
            return;
        }
        foreach (var c in commits)
        {
            var label = c.Breaking ? "BREAKING" : c.Type;
            var scope = c.Scope is not null ? $"({c.Scope})" : "";
            Console.WriteLine($"  {label}{scope}: {c.Description}");
        }
    }

    private static void CreateGitTag(string repoPath, string tag)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("tag");
        psi.ArgumentList.Add("-a");
        psi.ArgumentList.Add(tag);
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add($"Release {tag}");
        using var proc = Process.Start(psi)!;
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
            Console.Error.WriteLine($"  Warning: git tag failed — {stderr.Trim()}");
    }

    private static string ResolveTag(string format, string version, string repoName)
        => format.Replace("{version}", version).Replace("{repo}", repoName);

    private static string BuildTagGlob(string format, string repoName)
        => format.Replace("{version}", "*").Replace("{repo}", repoName);

    private static string? ExtractVersion(string? tag, string format, string repoName)
    {
        if (tag is null) return null;
        var vIdx = format.IndexOf("{version}", StringComparison.Ordinal);
        var prefix = format[..vIdx].Replace("{repo}", repoName);
        var suffix = format[(vIdx + "{version}".Length)..].Replace("{repo}", repoName);
        if (!tag.StartsWith(prefix, StringComparison.Ordinal)
            || !tag.EndsWith(suffix, StringComparison.Ordinal))
            return tag;
        var start = prefix.Length;
        var end   = tag.Length - suffix.Length;
        return start <= end ? tag[start..end] : tag;
    }
}
