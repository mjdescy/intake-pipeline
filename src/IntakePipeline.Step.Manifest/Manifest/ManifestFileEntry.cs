namespace IntakePipeline.Step.Manifest.Manifest;

/// <summary>
/// Describes a single file discovered under the manifested folder: where it
/// lives, its name and extension, its content hash and size, and any error
/// that prevented it from being described. Failed files keep their path and
/// name, plus hash/size when they could be read.
/// </summary>
/// <param name="FilePath">Absolute path of the file.</param>
/// <param name="FileName">Name of the file, including its extension.</param>
/// <param name="Sha256">SHA-256 hash of the file as lowercase hex, or null when it could not be hashed.</param>
/// <param name="FileSizeInBytes">Size of the file in bytes, or null when it could not be read.</param>
/// <param name="FileExtension">File extension including the leading dot (e.g. ".txt"), or an empty string when the file has none.</param>
/// <param name="Error">Error message for this file, or null when it was described cleanly.</param>
public sealed record ManifestFileEntry(
    string FilePath,
    string FileName,
    string? Sha256,
    long? FileSizeInBytes,
    string FileExtension,
    string? Error);
