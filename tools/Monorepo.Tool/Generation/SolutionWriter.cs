using System.Security.Cryptography;
using System.Text;
using Monorepo.Tool.Model;

namespace Monorepo.Tool.Generation;

/// <summary>
/// Generates a Visual Studio .sln file that spans all leaf repos.
/// Projects are grouped into nested solution folders that mirror the directory tree:
///   backend/core/common/src/Foo/Foo.csproj → solution folder core > common > Foo.csproj
///
/// GUIDs are derived deterministically from the item's path so the file is stable
/// across repeated regenerations (no spurious git diffs).
/// </summary>
public static class SolutionWriter
{
    private const string CsprojType = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";
    private const string FolderType = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

    public static void Write(
        string                   slnPath,
        string                   backendRoot,
        IReadOnlyList<RepoEntry> repos,
        bool                     dryRun = false)
    {
        var slnDir = Path.GetDirectoryName(slnPath)!;

        // ── Collect grouping dirs, repo folders, and csproj projects ─────────

        // key: grouping dir name (e.g. "core"); value: stable GUID
        var groupingFolders = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        // key: repo path (e.g. "core/common"); value: stable GUID
        var repoFolders = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        // all discovered csproj projects
        var projects = new List<ProjectEntry>();

        foreach (var repo in repos)
        {
            var parts      = repo.Path.Split('/');
            var groupDir   = parts.Length > 1 ? parts[0] : "";
            var repoName   = parts[^1];

            if (groupDir.Length > 0 && !groupingFolders.ContainsKey(groupDir))
                groupingFolders[groupDir] = Stable("folder:" + groupDir);

            repoFolders[repo.Path] = Stable("folder:" + repo.Path);

            var repoDir = Path.Combine(backendRoot, repo.Path.Replace('/', Path.DirectorySeparatorChar));
            foreach (var csproj in Monorepo.Tool.Discovery.FileSystemHelpers.EnumerateCsprojs(repoDir))
            {
                var relPath = Path.GetRelativePath(slnDir, csproj).Replace('\\', '/');
                projects.Add(new ProjectEntry(
                    Name:     Path.GetFileNameWithoutExtension(csproj),
                    RelPath:  relPath,
                    Guid:     Stable("project:" + relPath.ToLowerInvariant()),
                    RepoPath: repo.Path));
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio Version 17");
        sb.AppendLine("VisualStudioVersion = 17.8.34525.116");
        sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

        // Solution folder entries — grouping dirs
        foreach (var (name, guid) in groupingFolders)
            AppendFolder(sb, name, guid);

        // Solution folder entries — repo dirs (and __tools)
        foreach (var (path, guid) in repoFolders)
        {
            var displayName = path == "__tools" ? "tools" : path.Split('/')[^1];
            AppendFolder(sb, displayName, guid);
        }

        foreach (var p in projects)
            AppendProject(sb, p);

        sb.AppendLine("Global");

        sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        sb.AppendLine("\tEndGlobalSection");

        sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        foreach (var p in projects)
        {
            var g = G(p.Guid);
            sb.AppendLine($"\t\t{g}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            sb.AppendLine($"\t\t{g}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            sb.AppendLine($"\t\t{g}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            sb.AppendLine($"\t\t{g}.Release|Any CPU.Build.0 = Release|Any CPU");
        }
        sb.AppendLine("\tEndGlobalSection");

        sb.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
        sb.AppendLine("\t\tHideSolutionNode = FALSE");
        sb.AppendLine("\tEndGlobalSection");

        // Nesting: repoFolder → groupingFolder, project → repoFolder
        sb.AppendLine("\tGlobalSection(NestedProjects) = preSolution");

        foreach (var (repoPath, repoGuid) in repoFolders)
        {
            if (repoPath == "__tools") continue;
            var parts = repoPath.Split('/');
            if (parts.Length > 1 && groupingFolders.TryGetValue(parts[0], out var parentGuid))
                sb.AppendLine($"\t\t{G(repoGuid)} = {G(parentGuid)}");
        }

        foreach (var p in projects)
        {
            if (repoFolders.TryGetValue(p.RepoPath, out var parentGuid))
                sb.AppendLine($"\t\t{G(p.Guid)} = {G(parentGuid)}");
        }

        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("EndGlobal");

        Monorepo.Tool.IO.AtomicFile.WriteAllTextIfChanged(slnPath, sb.ToString(), dryRun);
    }

    private sealed record ProjectEntry(string Name, string RelPath, Guid Guid, string RepoPath);

    private static void AppendFolder(StringBuilder sb, string name, Guid guid) =>
        sb.AppendLine($"Project(\"{{{FolderType}}}\") = \"{name}\", \"{name}\", \"{G(guid)}\"")
          .AppendLine("EndProject");

    private static void AppendProject(StringBuilder sb, ProjectEntry p) =>
        sb.AppendLine($"Project(\"{{{CsprojType}}}\") = \"{p.Name}\", \"{p.RelPath.Replace('/', '\\')}\", \"{G(p.Guid)}\"")
          .AppendLine("EndProject");

    /// <summary>Formats a Guid as {XXXXXXXX-...} (uppercase, with braces) as expected by .sln.</summary>
    private static string G(Guid g) => $"{{{g.ToString().ToUpperInvariant()}}}";

    /// <summary>Derives a stable, deterministic GUID from a string seed via MD5.</summary>
    private static Guid Stable(string seed)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash);
    }
}