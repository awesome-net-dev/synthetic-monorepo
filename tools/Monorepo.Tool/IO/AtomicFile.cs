namespace Monorepo.Tool.IO;

/// <summary>
/// Filesystem writes that are atomic from the consumer's point of view:
/// the destination either has its previous content or the new content — never partial.
/// Implemented by writing to a sibling .tmp file then renaming over the target.
/// </summary>
public static class AtomicFile
{
    public static void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, content);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    /// <summary>
    /// Writes content atomically unless the file already has identical content.
    /// Logs progress to stdout with the verbs Unchanged / [dry-run] Would write / Written.
    /// Returns true when the file was (or would be) written.
    /// </summary>
    public static bool WriteAllTextIfChanged(string path, string content, bool dryRun)
    {
        if (File.Exists(path) && File.ReadAllText(path) == content)
        {
            Console.WriteLine($"  Unchanged: {path}");
            return false;
        }
        if (dryRun)
        {
            Console.WriteLine($"  [dry-run] Would write: {path}");
            return false;
        }
        WriteAllText(path, content);
        Console.WriteLine($"  Written:   {path}");
        return true;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
    }
}
