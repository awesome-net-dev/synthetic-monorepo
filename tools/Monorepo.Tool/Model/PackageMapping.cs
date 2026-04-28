namespace Monorepo.Tool.Model;

public sealed class PackageMapping
{
    /// <summary>NuGet package id being rewritten (e.g. "Foo.Bar").</summary>
    public string PackageId { get; set; } = "";

    /// <summary>Path to the producing .csproj, relative to backend root.</summary>
    public string CsprojPath { get; set; } = "";

    /// <summary>When false the rewrite target is omitted from the overlay but the mapping is preserved.</summary>
    public bool Enabled { get; set; } = true;
}
