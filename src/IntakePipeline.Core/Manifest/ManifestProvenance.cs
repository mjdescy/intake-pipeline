namespace IntakePipeline.Core.Manifest;

/// <summary>
/// Records where a manifest came from: which step produced it and, when it was
/// derived from another manifest, which one. This is the link that lets a
/// pipeline trace data lineage across steps. Null for a manifest that is not
/// derived from another, such as the first one in a pipeline.
/// </summary>
/// <param name="Step">Name of the step that produced the manifest; see <see cref="ManifestSteps"/>.</param>
/// <param name="ParentRunId">Run id of the manifest this one was derived from, or null.</param>
/// <param name="ParentManifestPath">Absolute path of the parent manifest, or null.</param>
public sealed record ManifestProvenance(
    string Step,
    Guid? ParentRunId,
    string? ParentManifestPath);
