using System.CommandLine;
using Monorepo.Tool.Generation;
using Monorepo.Tool.IO;
using Monorepo.Tool.Serialization;

namespace Monorepo.Tool.Commands;

public static class OnOffCommand
{
    public static Command BuildOn()  => Build("on",  "Create the .monorepo-active sentinel to enable the MSBuild overlay.",  activate: true);
    public static Command BuildOff() => Build("off", "Remove the .monorepo-active sentinel to disable the MSBuild overlay.", activate: false);

    private static Command Build(string name, string description, bool activate)
    {
        var configOpt = new Option<FileInfo?>("--config")
        {
            Description = "Explicit path to monorepo.json. Defaults to walk-up from CWD.",
        };

        var cmd = new Command(name, description) { configOpt };
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

            if (activate) SentinelManager.Activate(backendRoot);
            else          SentinelManager.Deactivate(backendRoot);
            return 0;
        });
        return cmd;
    }
}
