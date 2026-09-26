using System.Security.Cryptography;
using IntakePipeline.Core.Manifest;

namespace IntakePipeline.Step.Ingest;

/// <summary>
/// Copies every file listed in an input manifest to a destination folder,
/// preserving the relative folder structure, and returns a manifest describing
/// the copied files. Source files are left in place: this step copies, it never
/// moves. Each copied file is verified against the source hash recorded in the
/// input manifest, which also catches a source that changed after it was
/// manifested.
/// </summary>
public static class IngestRunner
{
    /// <summary>
    /// Runs the ingest of <paramref name="source"/> into
    /// <paramref name="destinationFolder"/>. Both folders must be absolute,
    /// resolved paths. The returned manifest's provenance links it to
    /// <paramref name="source"/>.
    /// </summary>
    public static FileManifest Run(FileManifest source, string destinationFolder, Guid runId, string? parentManifestPath)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);

        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        List<string> errors = [];
        List<ManifestFileEntry> entries = [];

        string sourceFolder = source.Folder;
        string destinationRoot = destinationFolder;

        if (IsUnderRoot(sourceFolder, destinationRoot) || IsUnderRoot(destinationRoot, sourceFolder))
        {
            errors.Add(
                $"Destination folder '{destinationRoot}' must not overlap the source folder '{sourceFolder}'; choose a destination outside the source tree.");
        }
        else
        {
            try
            {
                // Create the destination up front so an unusable path fails
                // fast, before any file is copied.
                Directory.CreateDirectory(destinationRoot);
            }
            catch (Exception ex)
            {
                errors.Add($"Destination folder could not be created: '{destinationRoot}' ({ex.Message}).");
            }
        }

        if (errors.Count == 0)
        {
            List<PlannedCopy> plans = [.. source.Entries.Select(entry => PlanCopy(entry, sourceFolder, destinationRoot))];
            List<string> duplicates = FindDuplicateDestinations(plans);
            if (duplicates.Count > 0)
            {
                errors.Add(
                    $"Manifest lists more than one file for the same destination: {string.Join(", ", duplicates)}.");
            }
            else
            {
                foreach (PlannedCopy plan in plans)
                {
                    entries.Add(plan.Error is null ? CopyFile(plan, destinationRoot) : FailureEntry(plan, plan.Error));
                }
            }
        }

        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        bool succeeded = errors.Count == 0 && entries.All(entry => entry.Error is null);
        return new FileManifest(
            runId,
            destinationRoot,
            startedAtUtc,
            completedAtUtc,
            succeeded ? ManifestStatus.Success : ManifestStatus.Failure,
            entries,
            errors,
            new ManifestProvenance(ManifestSteps.Ingest, source.RunId, parentManifestPath));
    }

    /// <summary>
    /// Works out where one entry will land and whether the mapping is safe.
    /// Anything wrong with the mapping becomes the plan's error; one bad entry
    /// never stops the run.
    /// </summary>
    private static PlannedCopy PlanCopy(ManifestFileEntry entry, string sourceFolder, string destinationRoot)
    {
        string fileName = string.IsNullOrWhiteSpace(entry.FileName)
            ? Path.GetFileName(entry.FilePath)
            : entry.FileName;
        string fileExtension = entry.FileExtension ?? "";

        if (entry.Error is not null)
        {
            // An entry the upstream step could not describe cannot be ingested;
            // carry its error forward so it stays visible downstream.
            return new PlannedCopy(entry, entry.FilePath, fileName, fileExtension, $"skipped: {entry.Error}");
        }

        if (string.IsNullOrWhiteSpace(entry.FilePath) || !Path.IsPathFullyQualified(entry.FilePath))
        {
            return new PlannedCopy(entry, entry.FilePath, fileName, fileExtension,
                $"Entry path is not absolute: '{entry.FilePath}'.");
        }

        string destinationFilePath;
        try
        {
            // Canonicalizing neutralizes "..", rooted-relative paths, and
            // cross-volume returns, so the containment test below is sound.
            destinationFilePath = Path.GetFullPath(
                Path.Combine(destinationRoot, Path.GetRelativePath(sourceFolder, entry.FilePath)));
        }
        catch (Exception ex)
        {
            return new PlannedCopy(entry, entry.FilePath, fileName, fileExtension,
                $"Entry path could not be mapped to the destination: '{entry.FilePath}' ({ex.Message}).");
        }

        if (string.Equals(destinationFilePath, destinationRoot, PathComparison)
            || !IsUnderRoot(destinationRoot, destinationFilePath))
        {
            return new PlannedCopy(entry, destinationFilePath, fileName, fileExtension,
                $"Entry path escapes the manifested folder: '{entry.FilePath}'.");
        }

        return new PlannedCopy(entry, destinationFilePath, fileName, fileExtension, null);
    }

    /// <summary>
    /// Copies one planned file and returns its entry. One bad file never stops
    /// the run: the exception becomes the entry's error.
    /// </summary>
    private static ManifestFileEntry CopyFile(PlannedCopy plan, string destinationRoot)
    {
        try
        {
            if (IsReparsePoint(plan.Source.FilePath))
            {
                return FailureEntry(plan,
                    $"Source entry is a symbolic link or junction; refusing to follow it: '{plan.Source.FilePath}'.");
            }

            if (HasReparsePointAncestor(destinationRoot, plan.DestinationFilePath))
            {
                return FailureEntry(plan,
                    $"Destination path has a symbolic link in its folders; refusing to write through it: '{plan.DestinationFilePath}'.");
            }

            string? destinationDirectory = Path.GetDirectoryName(plan.DestinationFilePath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Overwrite on purpose so re-running an ingest is idempotent,
            // e.g. when recovering from a partial failure.
            File.Copy(plan.Source.FilePath, plan.DestinationFilePath, overwrite: true);

            string destinationSha256 = ComputeSha256(plan.DestinationFilePath);
            long fileSizeInBytes = new FileInfo(plan.DestinationFilePath).Length;

            // The input manifest's hash is the baseline: a mismatch means the
            // copy is corrupt, or the source changed since it was manifested.
            if (plan.Source.Sha256 is not null
                && !string.Equals(plan.Source.Sha256, destinationSha256, StringComparison.Ordinal))
            {
                return FailureEntry(plan,
                    $"Copy verification failed for '{plan.Source.FilePath}': the copied file does not match the manifest hash (the source may have changed since it was manifested).");
            }

            return new ManifestFileEntry(
                plan.DestinationFilePath, plan.FileName, destinationSha256, fileSizeInBytes, plan.FileExtension, null);
        }
        catch (Exception ex)
        {
            return FailureEntry(plan, $"Failed to ingest '{plan.Source.FilePath}': {ex.Message}");
        }
    }

    private static ManifestFileEntry FailureEntry(PlannedCopy plan, string error) =>
        new(plan.DestinationFilePath, plan.FileName, null, null, plan.FileExtension, error);

    /// <summary>
    /// Returns the destination paths claimed by more than one entry. Two
    /// entries writing the same file would leave the earlier entry's verified
    /// hash stale, so the run is refused instead.
    /// </summary>
    private static List<string> FindDuplicateDestinations(List<PlannedCopy> plans)
    {
        HashSet<string> seen = new(PathComparer);
        List<string> duplicates = [];
        foreach (PlannedCopy plan in plans)
        {
            if (plan.Error is not null || string.IsNullOrEmpty(plan.DestinationFilePath))
            {
                continue;
            }

            if (!seen.Add(plan.DestinationFilePath))
            {
                duplicates.Add(plan.DestinationFilePath);
            }
        }

        return duplicates;
    }

    /// <summary>
    /// Returns true when <paramref name="path"/> is the root itself or lies
    /// below it. Both paths are expected to be canonical.
    /// </summary>
    private static bool IsUnderRoot(string root, string path) =>
        string.Equals(root, path, PathComparison)
        || path.StartsWith(EnsureTrailingSeparator(root), PathComparison);

    private static string EnsureTrailingSeparator(string folderPath) =>
        folderPath.EndsWith(Path.DirectorySeparatorChar) || folderPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? folderPath
            : folderPath + Path.DirectorySeparatorChar;

    /// <summary>
    /// True when a symlink or junction sits between
    /// <paramref name="destinationRoot"/> (exclusive) and the destination file.
    /// The root itself is not checked, so a symlinked destination folder is
    /// still allowed.
    /// </summary>
    private static bool HasReparsePointAncestor(string destinationRoot, string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory) && !string.Equals(directory, destinationRoot, PathComparison))
        {
            if (Directory.Exists(directory) && IsReparsePoint(directory))
            {
                return true;
            }

            string? parent = Path.GetDirectoryName(directory);
            if (parent is null || string.Equals(parent, directory, PathComparison))
            {
                break;
            }

            directory = parent;
        }

        return false;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        // Lowercase hex matches the customary checksum output on Linux (sha256sum).
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    // Case-insensitive where the filesystem is, so duplicate and containment
    // checks match the platform's actual path semantics.
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed record PlannedCopy(
        ManifestFileEntry Source,
        string DestinationFilePath,
        string FileName,
        string FileExtension,
        string? Error);
}
