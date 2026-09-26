using System.Security.Cryptography;
using IntakePipeline.Step.Ingest.Manifest;

namespace IntakePipeline.Step.Ingest;

/// <summary>
/// Copies every file under a source folder to a destination folder, preserving
/// the folder structure, and records the outcome in a result manifest. Source
/// files are left in place: this step copies, it never moves.
/// </summary>
public static class IngestRunner
{
    /// <summary>
    /// Runs the ingest described by <paramref name="manifest"/>. The manifest
    /// must contain absolute, resolved paths; see
    /// <see cref="ManifestIO.ReadIngestManifest(string)"/>.
    /// </summary>
    public static IngestResultManifest Run(IngestManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        List<string> errors = [];

        List<string> sourceFiles = EnumerateSourceFiles(manifest.SourceFolder, manifest.DestinationFolder, errors);
        List<IngestResultEntry> entries = [];
        foreach (string sourceFilePath in sourceFiles)
        {
            string relativePath = Path.GetRelativePath(manifest.SourceFolder, sourceFilePath);
            string destinationFilePath = Path.Combine(manifest.DestinationFolder, relativePath);
            entries.Add(IngestFile(sourceFilePath, destinationFilePath));
        }

        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        bool succeeded = errors.Count == 0 && entries.All(entry => entry.Error is null);
        return new IngestResultManifest(
            manifest.SourceFolder,
            manifest.DestinationFolder,
            startedAtUtc,
            completedAtUtc,
            succeeded ? ManifestStatus.Success : ManifestStatus.Failure,
            entries,
            errors);
    }

    /// <summary>
    /// Validates the folders, then returns a deterministic snapshot of the
    /// files to ingest. Any validation or enumeration failure is recorded as a
    /// global error and yields an empty list.
    /// </summary>
    private static List<string> EnumerateSourceFiles(string sourceFolder, string destinationFolder, List<string> errors)
    {
        if (!Directory.Exists(sourceFolder))
        {
            errors.Add($"Source folder not found: '{sourceFolder}'.");
            return [];
        }

        if (IsDestinationInsideSource(sourceFolder, destinationFolder))
        {
            errors.Add(
                $"Destination folder '{destinationFolder}' must not be inside the source folder '{sourceFolder}'; choose a destination outside the source tree.");
            return [];
        }

        try
        {
            // Create the destination up front so an unusable path fails fast,
            // before any file is copied.
            Directory.CreateDirectory(destinationFolder);
        }
        catch (Exception ex)
        {
            errors.Add($"Destination folder could not be created: '{destinationFolder}' ({ex.Message}).");
            return [];
        }

        try
        {
            // Snapshot and sort up front: a deterministic order makes manifests
            // comparable across runs and avoids mutating while enumerating.
            return [.. Directory
                .EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)];
        }
        catch (Exception ex)
        {
            errors.Add($"Source folder could not be enumerated: '{sourceFolder}' ({ex.Message}).");
            return [];
        }
    }

    /// <summary>
    /// Copies one file and returns its entry. One bad file never stops the
    /// run: the exception becomes the entry's error.
    /// </summary>
    private static IngestResultEntry IngestFile(string sourceFilePath, string destinationFilePath)
    {
        string? sha256 = null;
        long? fileSizeInBytes = null;
        try
        {
            fileSizeInBytes = new FileInfo(sourceFilePath).Length;
            sha256 = ComputeSha256(sourceFilePath);

            string? destinationDirectory = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Overwrite on purpose so re-running an ingest is idempotent,
            // e.g. when recovering from a partial failure.
            File.Copy(sourceFilePath, destinationFilePath, overwrite: true);

            // Comparing hashes after the copy catches torn or corrupt writes
            // that a successful copy call would otherwise hide.
            string destinationSha256 = ComputeSha256(destinationFilePath);
            if (!string.Equals(sha256, destinationSha256, StringComparison.Ordinal))
            {
                return new IngestResultEntry(
                    sourceFilePath,
                    destinationFilePath,
                    sha256,
                    fileSizeInBytes,
                    $"Copy verification failed for '{sourceFilePath}': destination hash mismatch.");
            }

            return new IngestResultEntry(sourceFilePath, destinationFilePath, sha256, fileSizeInBytes, null);
        }
        catch (Exception ex)
        {
            return new IngestResultEntry(
                sourceFilePath,
                destinationFilePath,
                sha256,
                fileSizeInBytes,
                $"Failed to ingest '{sourceFilePath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Returns true when the destination is the source folder or nested inside
    /// it, which would copy the source onto itself.
    /// </summary>
    private static bool IsDestinationInsideSource(string sourceFolder, string destinationFolder)
    {
        // OrdinalIgnoreCase is the safe choice on Windows, where paths that
        // differ only by case are the same folder; on case-sensitive filesystems
        // it can only over-block exotic case-only layouts.
        string sourceWithSeparator = EnsureTrailingSeparator(sourceFolder);
        string destinationWithSeparator = EnsureTrailingSeparator(destinationFolder);

        return string.Equals(sourceWithSeparator, destinationWithSeparator, StringComparison.OrdinalIgnoreCase)
            || destinationWithSeparator.StartsWith(sourceWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string folderPath) =>
        folderPath.EndsWith(Path.DirectorySeparatorChar) || folderPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? folderPath
            : folderPath + Path.DirectorySeparatorChar;

    private static string ComputeSha256(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        // Lowercase hex matches the customary checksum output on Linux (sha256sum).
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
