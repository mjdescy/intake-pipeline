using System.Text.Json;
using IntakePipeline.Core.Manifest;

namespace IntakePipeline.Step.Ingest.Tests;

/// <summary>
/// Helpers for writing input file manifests during tests. The JSON is produced
/// with <see cref="ManifestIO"/>, so it matches exactly what the pipeline steps
/// write and read.
/// </summary>
internal static class TestManifests
{
    internal static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Writes <paramref name="manifest"/> as JSON into
    /// <paramref name="directory"/> and returns the full path.
    /// </summary>
    internal static string Write(FileManifest manifest, string directory, string fileName = "manifest.json")
    {
        string path = Path.Combine(directory, fileName);
        ManifestIO.WriteFileManifest(manifest, path);
        return path;
    }
}
