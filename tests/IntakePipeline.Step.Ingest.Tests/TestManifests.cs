using System.Text.Json;

namespace IntakePipeline.Step.Ingest.Tests;

/// <summary>
/// Helpers for writing input manifests during tests. The JSON is produced
/// with System.Text.Json so path escaping is always correct.
/// </summary>
internal static class TestManifests
{
    internal static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Writes an input manifest into <paramref name="manifestDirectory"/>
    /// with the given (usually relative) folder paths, and returns its path.
    /// </summary>
    internal static string Write(string manifestDirectory, string sourceFolder, string destinationFolder)
    {
        string path = Path.Combine(manifestDirectory, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(
            new { sourceFolder, destinationFolder }, CamelCase));
        return path;
    }
}
