using System.Text.Json;

namespace IntakePipeline.Core.Tests;

/// <summary>
/// JSON options used to read manifests produced by <c>ManifestIO</c> during
/// tests. The camelCase policy mirrors the serializer's own options.
/// </summary>
internal static class TestJson
{
    internal static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
