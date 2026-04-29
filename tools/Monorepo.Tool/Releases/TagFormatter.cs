namespace Monorepo.Tool.Releases;

public static class TagFormatter
{
    public static string Resolve(string format, string version, string repoName)
        => format.Replace("{version}", version).Replace("{repo}", repoName);

    public static string ToGlob(string format, string repoName)
        => format.Replace("{version}", "*").Replace("{repo}", repoName);

    public static string? ExtractVersion(string? tag, string format, string repoName)
    {
        if (tag is null) return null;
        var vIdx   = format.IndexOf("{version}", StringComparison.Ordinal);
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
