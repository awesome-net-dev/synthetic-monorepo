using System.CommandLine;
using Monorepo.Tool.Generation;
using Monorepo.Tool.IO;
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

        cmd.SetAction(parseResult =>
        {
            var configFile = parseResult.GetValue(configOpt);
            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                CliOutput.Error("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)ExitCode.ConfigNotFound;
            }

            var config = ConfigSerializer.Load(configPath);
            var backendRoot = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(configPath)!,
                    config.BackendRoot.Replace('/', Path.DirectorySeparatorChar)));
            var overlayDir = Path.Combine(Path.GetDirectoryName(configPath)!, "overlay");
            var isOn       = SentinelManager.IsActive(backendRoot);

            Console.WriteLine();
            CliOutput.Header($"Monorepo overlay : {(isOn ? "ON" : "OFF")}");
            Console.WriteLine();
            CliOutput.Muted($"Backend root : {backendRoot}");
            CliOutput.Muted($"Config       : {configPath}");
            Console.WriteLine();

            // ── repos ──────────────────────────────────────────────────
            var total  = config.Repos.Count;
            var exempt = config.Repos.Count(r => r.Exempt);
            CliOutput.Info($"Repos ({total} total, {exempt} exempt):");
            foreach (var repo in config.Repos)
            {
                if (repo.Exempt)
                    CliOutput.Warning($"  ✗ {repo.Path}  EXEMPT — {repo.ExemptReason}");
                else
                    CliOutput.Success($"  ✓ {repo.Path}");
            }

            Console.WriteLine();

            // ── mappings ───────────────────────────────────────────────
            var enabled  = config.Mappings.Count(m => m.Enabled);
            var disabled = config.Mappings.Count(m => !m.Enabled);
            CliOutput.Info($"Mappings ({enabled} enabled, {disabled} disabled):");

            var missing = 0;
            foreach (var m in config.Mappings)
            {
                var csprojAbs = Path.GetFullPath(
                    Path.Combine(backendRoot, m.CsprojPath.Replace('/', Path.DirectorySeparatorChar)));
                var exists = File.Exists(csprojAbs);
                if (!exists) missing++;

                if (!m.Enabled)
                    CliOutput.Muted($"  ✗ {m.PackageId,-45} → DISABLED");
                else if (!exists)
                    CliOutput.Warning($"  ⚠ {m.PackageId,-45} → {m.CsprojPath}  FILE NOT FOUND");
                else
                    CliOutput.Success($"  ✓ {m.PackageId,-45} → {m.CsprojPath}");
            }

            Console.WriteLine();
            if (missing > 0)
                CliOutput.Warning($"Drift: {missing} csproj(s) missing on disk. Run 'monorepo generate --refresh'.");
            else
                CliOutput.Success("Drift: none detected.");

            Console.WriteLine();
            return missing > 0 ? (int)ExitCode.Drift : 0;
        });

        return cmd;
    }
}
