namespace Monorepo.Tool.Discovery;

/// <summary>
/// Shared filesystem enumeration helpers. Filters build-output and VCS-internal
/// directories so discovery and solution generation agree on a single source of truth.
/// </summary>
internal static class FileSystemHelpers
{
    private static readonly string[] ExcludedDirNames =
    [
        "bin", "obj", "node_modules", ".git", ".vs", ".idea",
    ];

    /// <summary>
    /// Enumerates *.csproj files under <paramref name="root"/> (recursively), skipping
    /// any path whose ancestry includes an excluded directory name.
    /// </summary>
    public static IEnumerable<string> EnumerateCsprojs(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        foreach (var path in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsExcluded(path, root))
                continue;
            yield return path;
        }
    }

    private static bool IsExcluded(string fullPath, string root)
    {
        var rel = Path.GetRelativePath(root, fullPath);
        foreach (var segment in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            foreach (var excluded in ExcludedDirNames)
                if (segment.Equals(excluded, StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        return false;
    }
}
