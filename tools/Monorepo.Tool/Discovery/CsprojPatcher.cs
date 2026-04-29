using System.Xml.Linq;
using Monorepo.Tool.IO;

namespace Monorepo.Tool.Discovery;

public static class CsprojPatcher
{
    public static void StripVersionAttributes(
        string       csprojPath,
        ISet<string> packageIds,
        bool         dryRun)
    {
        XDocument doc;
        using (var stream = File.OpenRead(csprojPath))
        {
            doc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }

        var modified = false;
        foreach (var el in (doc.Root?.Elements("ItemGroup") ?? []).Elements("PackageReference"))
        {
            var id = el.Attribute("Include")?.Value ?? "";
            if (!packageIds.Contains(id)) continue;
            var versionAttr = el.Attribute("Version");
            if (versionAttr is null) continue;
            versionAttr.Remove();
            modified = true;
        }

        if (!modified) return;

        var tempPath = Path.GetTempFileName();
        try
        {
            doc.Save(tempPath, SaveOptions.None);
            var newContent = File.ReadAllText(tempPath, System.Text.Encoding.UTF8);
            AtomicFile.WriteAllTextIfChanged(csprojPath, newContent, dryRun);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }
}
