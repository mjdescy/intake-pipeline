namespace IntakePipeline.Step.Ingest.Manifest;

/// <summary>
/// Manifest produced by an ingest run, describing every file discovered under
/// the source folder, any errors encountered, and the overall status.
/// </summary>
/// <param name="SourceFolder">Absolute path of the source folder.</param>
/// <param name="DestinationFolder">Absolute path of the destination folder.</param>
/// <param name="StartedAtUtc">UTC time the run started.</param>
/// <param name="CompletedAtUtc">UTC time the run completed.</param>
/// <param name="Status">Overall run status; see <see cref="ManifestStatus"/>.</param>
/// <param name="Entries">One entry per file discovered under the source folder.</param>
/// <param name="Errors">Global error messages, e.g. a missing source folder.</param>
public sealed record IngestResultManifest(
    string SourceFolder,
    string DestinationFolder,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Status,
    IReadOnlyList<IngestResultEntry> Entries,
    IReadOnlyList<string> Errors);
