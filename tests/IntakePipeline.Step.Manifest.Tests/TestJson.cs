using System.Text.Json;

namespace IntakePipeline.Step.Manifest.Tests;

/// <summary>
/// JSON options used to read manifests produced by this step during tests.
/// The camelCase policy mirrors the serializer used by <c>ManifestIO</c>.
/// </summary>
internal static class TestJson
{
    internal static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
