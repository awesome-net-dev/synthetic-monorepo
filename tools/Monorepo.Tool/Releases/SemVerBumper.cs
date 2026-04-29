namespace Monorepo.Tool.Releases;

public enum BumpType { Patch, Minor, Major }

public static class SemVerBumper
{
    public static (int Major, int Minor, int Patch) ParseVersion(string tag)
    {
        var clean = tag.TrimStart('v');
        var parts = clean.Split('.').Select(p =>
            int.TryParse(p.Split('-')[0], out var n) ? n : 0).ToArray();
        return (
            parts.Length > 0 ? parts[0] : 0,
            parts.Length > 1 ? parts[1] : 0,
            parts.Length > 2 ? parts[2] : 0);
    }

    public static string Bump(string currentVersion, BumpType type)
    {
        var (maj, min, pat) = ParseVersion(currentVersion);
        return type switch
        {
            BumpType.Major => $"{maj + 1}.0.0",
            BumpType.Minor => $"{maj}.{min + 1}.0",
            BumpType.Patch => $"{maj}.{min}.{pat + 1}",
            _              => throw new ArgumentOutOfRangeException(nameof(type)),
        };
    }

    public static BumpType DetermineFromCommits(IReadOnlyList<ConventionalCommit> commits)
    {
        if (commits.Any(c => c.Breaking)) return BumpType.Major;
        if (commits.Any(c => c.Type == "feat")) return BumpType.Minor;
        return BumpType.Patch;
    }
}
