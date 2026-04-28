namespace Monorepo.Tool.Generation;

public static class SentinelManager
{
    private const string FileName = ".monorepo-active";

    public static string SentinelPath(string backendRoot) =>
        Path.Combine(backendRoot, FileName);

    public static bool IsActive(string backendRoot) =>
        File.Exists(SentinelPath(backendRoot));

    public static void Activate(string backendRoot, bool dryRun = false)
    {
        var path = SentinelPath(backendRoot);
        if (dryRun)
        {
            Console.WriteLine($"  [dry-run] Would create: {path}");
            return;
        }
        Monorepo.Tool.IO.AtomicFile.WriteAllText(path, "");
        Console.WriteLine($"  Created:  {path}");
    }

    public static void Deactivate(string backendRoot, bool dryRun = false)
    {
        var path = SentinelPath(backendRoot);
        if (!File.Exists(path))
        {
            Console.WriteLine($"  Sentinel not present (already off): {path}");
            return;
        }
        if (dryRun)
        {
            Console.WriteLine($"  [dry-run] Would delete: {path}");
            return;
        }
        File.Delete(path);
        Console.WriteLine($"  Deleted:  {path}");
    }
}
