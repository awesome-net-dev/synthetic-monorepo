using Monorepo.Tool.Generation;
using Monorepo.Tool.Model;
using Xunit;

namespace Monorepo.Tool.Tests.Generation;

public class SolutionWriterTests
{
    [Fact]
    public void Generates_deterministic_output_across_regenerations()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("repo");
        fx.WriteCsproj(repo, "src/A.csproj", "<Project/>");
        var repos = new List<RepoEntry> { new() { Path = "repo" } };
        var slnPath = Path.Combine(fx.Root, "Monorepo.sln");

        SolutionWriter.Write(slnPath, fx.Root, repos);
        var first = File.ReadAllText(slnPath);
        SolutionWriter.Write(slnPath, fx.Root, repos);
        var second = File.ReadAllText(slnPath);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Includes_discovered_csprojs_as_solution_projects()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("svc");
        fx.WriteCsproj(repo, "src/Svc.csproj", "<Project/>");
        fx.WriteCsproj(repo, "tests/Svc.Tests.csproj", "<Project/>");
        var repos = new List<RepoEntry> { new() { Path = "svc" } };
        var slnPath = Path.Combine(fx.Root, "Monorepo.sln");

        SolutionWriter.Write(slnPath, fx.Root, repos);

        var text = File.ReadAllText(slnPath);
        Assert.Contains("Svc.csproj",       text);
        Assert.Contains("Svc.Tests.csproj", text);
    }

    [Fact]
    public void Skips_csprojs_under_bin_and_obj()
    {
        using var fx = new TempRepoFixture();
        var repo = fx.CreateRepo("svc");
        fx.WriteCsproj(repo, "src/Real.csproj",             "<Project/>");
        fx.WriteCsproj(repo, "obj/Debug/Generated.csproj",  "<Project/>");
        fx.WriteCsproj(repo, "bin/Release/Also.csproj",     "<Project/>");
        var repos = new List<RepoEntry> { new() { Path = "svc" } };
        var slnPath = Path.Combine(fx.Root, "Monorepo.sln");

        SolutionWriter.Write(slnPath, fx.Root, repos);

        var text = File.ReadAllText(slnPath);
        Assert.Contains("Real.csproj",        text);
        Assert.DoesNotContain("Generated.csproj", text);
        Assert.DoesNotContain("Also.csproj",      text);
    }

    [Fact]
    public void Includes_Monorepo_Tool_Tests_csproj_when_present_at_conventional_path()
    {
        using var fx = new TempRepoFixture();
        var repos = new List<RepoEntry>();
        var slnDir = Path.Combine(fx.Root, "synthetic-monorepo");
        Directory.CreateDirectory(slnDir);
        var toolDir  = Path.Combine(slnDir, "tools", "Monorepo.Tool");
        var testsDir = Path.Combine(slnDir, "tools", "Monorepo.Tool.Tests");
        Directory.CreateDirectory(toolDir);
        Directory.CreateDirectory(testsDir);
        File.WriteAllText(Path.Combine(toolDir,  "Monorepo.Tool.csproj"),       "<Project/>");
        File.WriteAllText(Path.Combine(testsDir, "Monorepo.Tool.Tests.csproj"), "<Project/>");

        SolutionWriter.Write(Path.Combine(slnDir, "Monorepo.sln"), fx.Root, repos);

        var text = File.ReadAllText(Path.Combine(slnDir, "Monorepo.sln"));
        Assert.Contains("Monorepo.Tool.csproj",       text);
        Assert.Contains("Monorepo.Tool.Tests.csproj", text);
    }
}
