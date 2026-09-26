namespace IntakePipeline.Core.Manifest;

/// <summary>
/// Step names recorded in manifest provenance. Kept as constants so that the
/// manifest JSON and the code never drift apart.
/// </summary>
public static class ManifestSteps
{
    /// <summary>The Manifest step, which describes a folder's files.</summary>
    public const string Manifest = "manifest";

    /// <summary>The Ingest step, which copies a manifest's files.</summary>
    public const string Ingest = "ingest";
}
