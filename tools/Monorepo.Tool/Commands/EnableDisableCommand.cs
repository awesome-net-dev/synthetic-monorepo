using System.CommandLine;
using Monorepo.Tool.IO;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class EnableDisableCommand
{
    public static Command BuildEnable()  => Build("enable",  setTo: true);
    public static Command BuildDisable() => Build("disable", setTo: false);

    private static Command Build(string name, bool setTo)
    {
        var packageArg = new Argument<string>("packageId") { Description = "PackageId to toggle (case-insensitive)." };
        var configOpt  = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walk-up from CWD.",
        };

        var cmd = new Command(name,
            $"Set Enabled={setTo.ToString().ToLowerInvariant()} for the given package mapping.")
        {
            packageArg,
            configOpt,
        };

        cmd.SetAction(parseResult =>
        {
            var pkg        = parseResult.GetValue(packageArg)!;
            var configFile = parseResult.GetValue(configOpt);
            var configPath = configFile?.FullName
                             ?? ConfigSerializer.Locate(Directory.GetCurrentDirectory());

            if (configPath is null)
            {
                Console.Error.WriteLine("Error: monorepo.json not found. Run 'monorepo init' first.");
                return (int)ExitCode.ConfigNotFound;
            }

            var config  = ConfigSerializer.Load(configPath);
            var mapping = config.Mappings
                .FirstOrDefault(m => string.Equals(m.PackageId, pkg, StringComparison.OrdinalIgnoreCase));

            if (mapping is null)
            {
                Console.Error.WriteLine(
                    $"Error: no mapping for '{pkg}' in {configPath}. " +
                    "Run 'monorepo status' to see the list.");
                return (int)ExitCode.InvalidInput;
            }

            if (mapping.Enabled == setTo)
            {
                Console.WriteLine($"'{mapping.PackageId}' already Enabled={setTo}. No change.");
                return 0;
            }

            mapping.Enabled = setTo;
            ConfigSerializer.Save(config, configPath);
            Console.WriteLine($"Set Enabled={setTo} for '{mapping.PackageId}'. " +
                              "Run 'monorepo generate' to regenerate the overlay.");
            return 0;
        });

        return cmd;
    }
}
