namespace IntakePipeline.Step.Ingest.Manifest;

/// <summary>
/// Input manifest that specifies the folders involved in an ingest run.
/// </summary>
/// <param name="SourceFolder">Folder to copy files from.</param>
/// <param name="DestinationFolder">Folder to copy files to.</param>
public sealed record IngestManifest(
    string SourceFolder,
    string DestinationFolder);
