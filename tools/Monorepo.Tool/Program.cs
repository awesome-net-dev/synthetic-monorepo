using System.CommandLine;
using Monorepo.Tool.Commands;
using Monorepo.Tool.IO;

namespace Monorepo.Tool;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await BuildRoot().Parse(args).InvokeAsync();
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return (int)ExitCode.ConfigNotFound;
        }
        catch (System.Text.Json.JsonException ex)
        {
            Console.Error.WriteLine($"Error: monorepo.json is corrupt — {ex.Message}");
            return (int)ExitCode.ConfigCorrupt;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled error: {ex.GetType().Name}: {ex.Message}");
            if (Environment.GetEnvironmentVariable("MONOREPO_DEBUG") == "1")
                Console.Error.WriteLine(ex);
            return (int)ExitCode.GeneralError;
        }
    }

    public static RootCommand BuildRoot()
    {
        var root = new RootCommand(
            "Synthetic monorepo overlay manager — rewrites PackageReferences to " +
            "ProjectReferences for sibling repos without modifying any leaf repo.")
        {
            InitCommand.Build(),
            GenerateCommand.Build(),
            OnOffCommand.BuildOn(),
            OnOffCommand.BuildOff(),
            StatusCommand.Build(),
            EnableDisableCommand.BuildEnable(),
            EnableDisableCommand.BuildDisable(),
            CpmCommand.Build(),
            ReleaseCommand.Build(),
            CleanCommand.Build(),
            BumpCommand.Build(),
            CloneCommand.Build()
        };

        root.SetAction(_ =>
        {
            Console.WriteLine(root.Description);
            Console.WriteLine();
            Console.WriteLine("Run 'monorepo --help' for available commands.");
        });

        return root;
    }
}
