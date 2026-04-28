using System.CommandLine;
using Monorepo.Tool.Generation;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class StatusCommand
{
    public static Command Build()
    {
        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. " +
                          "Defaults to the first monorepo.json found by walking up from the current directory.",
        };

        var cmd = new Command("status",
            "Show overlay state, active mappings, exempt repos, and disk drift.")
        {
            configOpt,
        };

        cmd.SetAction((ParseResult parseResult) =>
        {
            var configFile = parseResult.GetValue(configOpt);
            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                Console.Error.WriteLine("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)Monorepo.Tool.IO.ExitCode.ConfigNotFound;
            }

            var config = ConfigSerializer.Load(configPath);
            var backendRoot = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(configPath)!,
                             config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));
            var overlayDir = Path.Combine(Path.GetDirectoryName(configPath)!, "overlay");

            var isOn = SentinelManager.IsActive(backendRoot);

            Console.WriteLine();
            Console.WriteLine($"Monorepo overlay : {(isOn ? "ON  ✓" : "OFF ✗")}");
            Console.WriteLine($"Config           : {configPath}");
            Console.WriteLine($"Backend root     : {backendRoot}");
            Console.WriteLine($"Overlay dir      : {overlayDir}");
            Console.WriteLine();

            // Repos
            var total  = config.Repos.Count;
            var exempt = config.Repos.Count(r => r.Exempt);
            Console.WriteLine($"Repos ({total} total, {exempt} exempt):");
            foreach (var repo in config.Repos)
            {
                var marker = repo.Exempt ? "✗" : "✓";
                var suffix = repo.Exempt ? $"  EXEMPT — {repo.ExemptReason}" : "";
                Console.WriteLine($"  {marker} {repo.Path}{suffix}");
            }

            Console.WriteLine();

            // Mappings
            var enabled  = config.Mappings.Count(m => m.Enabled);
            var disabled = config.Mappings.Count(m => !m.Enabled);
            Console.WriteLine($"Mappings ({enabled} enabled, {disabled} disabled):");

            var missing = 0;
            foreach (var m in config.Mappings)
            {
                var marker    = m.Enabled ? "✓" : "✗";
                var csprojAbs = Path.GetFullPath(
                    Path.Combine(backendRoot, m.CsprojPath.Replace('/', Path.DirectorySeparatorChar)));
                var exists    = File.Exists(csprojAbs);
                if (!exists) missing++;
                var driftMark = exists ? "" : "  ⚠ CSPROJ NOT FOUND";
                Console.WriteLine($"  {marker} {m.PackageId,-45} → {m.CsprojPath}{driftMark}");
            }

            Console.WriteLine();
            if (missing > 0)
                Console.WriteLine($"⚠  Drift detected: {missing} csproj(s) missing on disk. Run 'monorepo generate --refresh'.");
            else
                Console.WriteLine("Drift: none detected.");

            Console.WriteLine();
            return missing > 0 ? (int)Monorepo.Tool.IO.ExitCode.Drift : 0;
        });

        return cmd;
    }
}
