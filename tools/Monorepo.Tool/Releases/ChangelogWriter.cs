using System.Text;
using Monorepo.Tool.IO;

namespace Monorepo.Tool.Releases;

public static class ChangelogWriter
{
    public static void Write(
        string                           repoPath,
        string                           version,
        IReadOnlyList<ConventionalCommit> commits,
        DateTime                         date,
        bool                             dryRun)
    {
        var entry = BuildEntry(version, commits, date);
        var changelogPath = Path.Combine(repoPath, "CHANGELOG.md");

        var existing = File.Exists(changelogPath) ? File.ReadAllText(changelogPath) : "";
        var content  = entry + (existing.Length > 0 ? "\n" + existing : "");

        AtomicFile.WriteAllTextIfChanged(changelogPath, content, dryRun);
    }

    private static string BuildEntry(
        string                           version,
        IReadOnlyList<ConventionalCommit> commits,
        DateTime                         date)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## [{version}] - {date:yyyy-MM-dd}");
        sb.AppendLine();

        AppendSection(sb, "Added",   commits.Where(c => c.Type == "feat"));
        AppendSection(sb, "Fixed",   commits.Where(c => c.Type == "fix"));
        AppendSection(sb, "Changed", commits.Where(c => c.Type is "refactor" or "perf" or "style"));
        AppendSection(sb, "Other",   commits.Where(c => c.Type is not ("feat" or "fix" or "refactor" or "perf" or "style")));

        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string title, IEnumerable<ConventionalCommit> commits)
    {
        var list = commits.ToList();
        if (list.Count == 0) return;

        sb.AppendLine($"### {title}");
        sb.AppendLine();
        foreach (var c in list)
            sb.AppendLine($"- {c.Description}");
        sb.AppendLine();
    }
}
