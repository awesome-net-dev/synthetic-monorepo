using System.Text;

namespace Monorepo.Tool.Tests;

/// <summary>
/// Creates a scratch directory under the OS temp path and recursively deletes it on Dispose.
/// Helpers let tests lay out a synthetic backend tree concisely.
/// </summary>
public sealed class TempRepoFixture : IDisposable
{
    public string Root { get; }

    public TempRepoFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "monorepo-tool-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string CreateRepo(string relativePath, bool ownsDirectoryBuildProps = false)
    {
        var dir = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, ".git"));
        if (ownsDirectoryBuildProps)
            File.WriteAllText(Path.Combine(dir, "Directory.Build.props"), "<Project/>");
        return dir;
    }

    public string WriteCsproj(string repoDir, string relativeCsprojPath, string xml)
    {
        var csproj = Path.Combine(repoDir, relativeCsprojPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(csproj)!);
        File.WriteAllText(csproj, xml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return csproj;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch
        {
            // best-effort cleanup — temp dir gets GC'd by the OS eventually
        }
    }
}
