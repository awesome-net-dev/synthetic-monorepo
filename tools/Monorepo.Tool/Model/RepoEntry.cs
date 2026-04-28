namespace Monorepo.Tool.Model;

public sealed class RepoEntry
{
    /// <summary>Path relative to backend root (e.g. "core/common").</summary>
    public string Path { get; set; } = "";

    /// <summary>True when the repo owns a Directory.Build.props — walk-up shim cannot reach it.</summary>
    public bool Exempt { get; set; }

    public string? ExemptReason { get; set; }

    /// <summary>PackageIds produced by csprojs inside this repo.</summary>
    public List<string> ProducedPackages { get; set; } = [];
}
