namespace IntakePipeline.Core.Manifest;

/// <summary>
/// The shared manifest describing a set of files and the run that produced it.
/// Every pipeline step can read or write this structure.
/// </summary>
/// <param name="RunId">Identifier supplied by the caller for this run.</param>
/// <param name="Folder">Absolute path of the folder that was manifested.</param>
/// <param name="StartedAtUtc">UTC time the run started.</param>
/// <param name="CompletedAtUtc">UTC time the run completed.</param>
/// <param name="Status">Overall run status; see <see cref="ManifestStatus"/>.</param>
/// <param name="Entries">One entry per file discovered under the folder.</param>
/// <param name="Errors">Global error messages, e.g. a missing source folder.</param>
public sealed record FileManifest(
    Guid RunId,
    string Folder,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Status,
    IReadOnlyList<ManifestFileEntry> Entries,
    IReadOnlyList<string> Errors);
