using System.Xml.Linq;

namespace Monorepo.Tool.Discovery;

/// <summary>
/// Reads package metadata from a .csproj file via XDocument (no MSBuild SDK required).
/// Queries are scoped to top-level &lt;PropertyGroup&gt; / &lt;ItemGroup&gt; so nested elements
/// inside &lt;Target&gt; tasks are ignored. MSBuild conditions and imports are NOT evaluated.
/// Known limitation: groups nested under &lt;Choose&gt;/&lt;When&gt; are not visited — put
/// unconditional &lt;ItemGroup&gt; / &lt;PropertyGroup&gt; at the project root for them to be seen.
/// </summary>
public static class CsprojReader
{
    /// <summary>
    /// Returns the effective package id for this project:
    ///   1. &lt;PackageId&gt; on a top-level &lt;PropertyGroup&gt;
    ///   2. &lt;AssemblyName&gt; on a top-level &lt;PropertyGroup&gt;
    ///   3. Filename stem
    /// </summary>
    public static string ReadPackageId(string csprojPath)
    {
        var doc = LoadXml(csprojPath);
        return TopLevelPropertyValue(doc, "PackageId")
               ?? TopLevelPropertyValue(doc, "AssemblyName")
               ?? Path.GetFileNameWithoutExtension(csprojPath);
    }

    /// <summary>
    /// Returns all PackageReference Include values declared directly under top-level
    /// &lt;ItemGroup&gt; elements (not inside &lt;Target&gt;). Skips references with
    /// PrivateAssets="all" (analyser / build-tool only). Case-insensitive dedup.
    /// </summary>
    public static IReadOnlyList<string> ReadPackageReferences(string csprojPath)
    {
        var doc = LoadXml(csprojPath);

        return (doc.Root?.Elements("ItemGroup") ?? [])
            .Elements("PackageReference")
            .Where(IsRuntimeReference)
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsRuntimeReference(XElement el)
    {
        var privateAssets = el.Attribute("PrivateAssets")?.Value
                           ?? el.Element("PrivateAssets")?.Value
                           ?? "";
        return !privateAssets.Equals("all", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TopLevelPropertyValue(XDocument doc, string elementName) =>
        (doc.Root?.Elements("PropertyGroup") ?? [])
            .Elements(elementName)
            .FirstOrDefault()
            ?.Value.Trim() is { Length: > 0 } v ? v : null;

    // Stream-based load: XDocument handles UTF-8 BOM transparently either way,
    // and explicit FileStream control lets us opt into FileShare.Read if needed later.
    private static XDocument LoadXml(string path)
    {
        using var stream = File.OpenRead(path);
        return XDocument.Load(stream, LoadOptions.None);
    }
}
