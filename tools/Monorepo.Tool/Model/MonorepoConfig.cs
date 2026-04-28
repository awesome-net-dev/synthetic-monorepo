namespace Monorepo.Tool.Model;

public sealed class MonorepoConfig
{
    public int Version { get; set; } = 1;

    /// <summary>Path to the backend root, relative to the directory containing monorepo.json.</summary>
    public string BackendRoot { get; set; } = "../..";

    public List<RepoEntry> Repos { get; set; } = [];
    public List<PackageMapping> Mappings { get; set; } = [];
}
