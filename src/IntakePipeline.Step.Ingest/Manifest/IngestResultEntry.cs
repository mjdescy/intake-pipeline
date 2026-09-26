namespace IntakePipeline.Step.Ingest.Manifest;

/// <summary>
/// Records the ingest of a single file: where it came from, where it landed,
/// its content hash and size, and any error that occurred. Failed files keep
/// their planned destination path, and hash/size when they could be read.
/// </summary>
/// <param name="SourceFilePath">Absolute path of the source file.</param>
/// <param name="DestinationFilePath">Absolute path of the destination file.</param>
/// <param name="Sha256">SHA-256 hash of the file as lowercase hex, or null when it could not be hashed.</param>
/// <param name="FileSizeInBytes">Size of the source file in bytes, or null when it could not be read.</param>
/// <param name="Error">Error message for this file, or null when it ingested cleanly.</param>
public sealed record IngestResultEntry(
    string SourceFilePath,
    string DestinationFilePath,
    string? Sha256,
    long? FileSizeInBytes,
    string? Error);
