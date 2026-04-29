using System.Text.RegularExpressions;

namespace Monorepo.Tool.Releases;

public sealed record ConventionalCommit(
    string  Type,
    string? Scope,
    bool    Breaking,
    string  Description);

public static partial class ConventionalCommitParser
{
    [GeneratedRegex(
        @"^(?<type>[a-zA-Z]+)(\((?<scope>[^)]+)\))?(?<bang>!)?:\s*(?<desc>.+)$",
        RegexOptions.Compiled)]
    private static partial Regex SubjectRegex();

    public static ConventionalCommit? Parse(string subject, string? body)
    {
        var m = SubjectRegex().Match(subject.Trim());
        if (!m.Success) return null;

        var type     = m.Groups["type"].Value.ToLowerInvariant();
        var scope    = m.Groups["scope"].Success ? m.Groups["scope"].Value : null;
        var breaking = m.Groups["bang"].Success
                       || (body?.Contains("BREAKING CHANGE:", StringComparison.Ordinal) ?? false);
        var desc     = m.Groups["desc"].Value.Trim();

        return new ConventionalCommit(type, scope, breaking, desc);
    }
}
