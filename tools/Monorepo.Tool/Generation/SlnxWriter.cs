using System.Text;
using Monorepo.Tool.Discovery;
using Monorepo.Tool.IO;
using Monorepo.Tool.Model;

namespace Monorepo.Tool.Generation;

public static class SlnxWriter
{
    public static void Write(
        string                   slnxPath,
        string                   backendRoot,
        IReadOnlyList<RepoEntry> repos,
        bool                     dryRun = false)
    {
        var slnxDir = Path.GetDirectoryName(slnxPath)!;
        var xml = BuildSlnx(slnxDir, backendRoot, repos);
        AtomicFile.WriteAllTextIfChanged(slnxPath, xml, dryRun);
    }

    internal static string BuildSlnx(
        string                   slnxDir,
        string                   backendRoot,
        IReadOnlyList<RepoEntry> repos)
    {
        // .slnx hierarchy is flat: all <Folder> elements are siblings; nesting is
        // encoded in the Name path (/group/ then /group/repo/), not in XML structure.
        var entries = repos
            .Select(r =>
            {
                var parts    = r.Path.Split('/');
                var group    = parts.Length > 1 ? parts[0] : null;
                var repoName = parts[^1];
                var repoDir  = Path.Combine(backendRoot, r.Path.Replace('/', Path.DirectorySeparatorChar));
                var csprojs  = FileSystemHelpers
                    .EnumerateCsprojs(repoDir)
                    .Select(p => Path.GetRelativePath(slnxDir, p).Replace('\\', '/'))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return (group, repoName, csprojs);
            })
            .Where(e => e.csprojs.Count > 0)
            .ToList();

        var grouped = entries
            .Where(e => e.group is not null)
            .GroupBy(e => e.group!)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ungrouped = entries
            .Where(e => e.group is null)
            .OrderBy(e => e.repoName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<Solution>");

        foreach (var group in grouped)
        {
            // Empty group folder — VS infers parent/child from path prefix.
            sb.AppendLine($"  <Folder Name=\"/{group.Key}/\" />");
            foreach (var (_, repoName, csprojs) in group.OrderBy(r => r.repoName, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  <Folder Name=\"/{group.Key}/{repoName}/\">");
                foreach (var proj in csprojs)
                    sb.AppendLine($"    <Project Path=\"{proj}\" />");
                sb.AppendLine($"  </Folder>");
            }
        }

        foreach (var (_, repoName, csprojs) in ungrouped)
        {
            sb.AppendLine($"  <Folder Name=\"/{repoName}/\">");
            foreach (var proj in csprojs)
                sb.AppendLine($"    <Project Path=\"{proj}\" />");
            sb.AppendLine($"  </Folder>");
        }

        sb.Append("</Solution>");
        return sb.ToString();
    }
}
