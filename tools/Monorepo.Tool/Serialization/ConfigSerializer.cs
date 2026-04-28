using System.Text.Json;
using System.Text.Json.Serialization;
using Monorepo.Tool.Model;

namespace Monorepo.Tool.Serialization;

public static class ConfigSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static MonorepoConfig Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<MonorepoConfig>(stream, Options)
               ?? throw new InvalidDataException($"monorepo.json at '{path}' deserialised to null.");
    }

    public static void Save(MonorepoConfig config, string path)
    {
        var json = JsonSerializer.Serialize(config, Options);
        Monorepo.Tool.IO.AtomicFile.WriteAllText(path, json);
    }

    /// <summary>
    /// Walks from <paramref name="startDir"/> toward the root looking for monorepo.json.
    /// Returns the full path if found, null otherwise.
    /// </summary>
    public static string? Locate(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "monorepo.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
