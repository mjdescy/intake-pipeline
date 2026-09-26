using System.Security.Cryptography;
using IntakePipeline.Core.Manifest;

namespace IntakePipeline.Step.Manifest;

/// <summary>
/// Builds a manifest describing every file under a folder, including all
/// subfolders. Each entry records the file's path, name, extension, size, and
/// SHA-256 hash. Files are read only: this step never changes them, and it
/// never follows symbolic links out of the folder being manifested.
/// </summary>
public static class ManifestRunner
{
    /// <summary>
    /// Runs the manifest described by a folder and run id. The folder must be
    /// an absolute, resolved path.
    /// </summary>
    public static FileManifest Run(Guid runId, string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
        List<string> errors = [];

        List<string> files = EnumerateFiles(folder, errors);
        List<ManifestFileEntry> entries = [.. files.Select(DescribeFile)];

        DateTimeOffset completedAtUtc = DateTimeOffset.UtcNow;
        bool succeeded = errors.Count == 0 && entries.All(entry => entry.Error is null);
        return new FileManifest(
            runId,
            folder,
            startedAtUtc,
            completedAtUtc,
            succeeded ? ManifestStatus.Success : ManifestStatus.Failure,
            entries,
            errors,
            new ManifestProvenance(ManifestSteps.Manifest, null, null));
    }

    /// <summary>
    /// Returns a deterministic snapshot of the files to describe. A missing
    /// folder is a global error; an unreadable subfolder is recorded as a
    /// global error and skipped so the rest of the tree is still described.
    /// </summary>
    private static List<string> EnumerateFiles(string folder, List<string> errors)
    {
        if (!Directory.Exists(folder))
        {
            errors.Add(MissingFolderError(folder));
            return [];
        }

        List<string> files = [];
        // Iterative walk rather than Directory.EnumerateFiles(AllDirectories):
        // it cannot overflow the stack on deep trees, it records unreadable
        // subfolders instead of aborting the whole run, and it lets us skip
        // reparse points (symlinks and junctions).
        Stack<string> pending = new();
        pending.Push(folder);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();

            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(directory);
            }
            catch (Exception ex)
            {
                errors.Add($"Folder could not be enumerated: '{directory}' ({ex.Message}).");
                continue;
            }

            foreach (string entry in entries)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (Exception ex)
                {
                    errors.Add($"Entry could not be inspected: '{entry}' ({ex.Message}).");
                    continue;
                }

                // Never follow a symlink or junction: its target is not part
                // of the folder being manifested, and cycles would loop.
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
                else
                {
                    files.Add(entry);
                }
            }
        }

        // Sort after collecting: a deterministic order makes manifests
        // comparable across runs.
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    /// <summary>
    /// Describes one file. One bad file never stops the run: the exception
    /// becomes the entry's error.
    /// </summary>
    private static ManifestFileEntry DescribeFile(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string fileExtension = FileExtensionOf(fileName);
        try
        {
            // Read the file once: the size and the hash then describe the same
            // content, with no window for the file to change in between.
            using FileStream stream = File.OpenRead(filePath);
            long fileSizeInBytes = stream.Length;
            // Lowercase hex matches the customary checksum output on Linux (sha256sum).
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));
            return new ManifestFileEntry(filePath, fileName, sha256, fileSizeInBytes, fileExtension, null);
        }
        catch (Exception ex)
        {
            return new ManifestFileEntry(
                filePath,
                fileName,
                null,
                null,
                fileExtension,
                $"Failed to describe '{filePath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Reports a folder as missing, or as unreadable when that is the real
    /// reason <see cref="Directory.Exists(string)"/> returned false.
    /// </summary>
    private static string MissingFolderError(string folder)
    {
        try
        {
            _ = Directory.GetFileSystemEntries(folder);
            return $"Folder not found: '{folder}'.";
        }
        catch (DirectoryNotFoundException)
        {
            return $"Folder not found: '{folder}'.";
        }
        catch (Exception ex)
        {
            return $"Folder could not be read: '{folder}' ({ex.Message}).";
        }
    }

    /// <summary>
    /// Returns the extension of a file name, or an empty string when it has
    /// none. A dotfile such as ".gitignore" is all name and no extension.
    /// </summary>
    private static string FileExtensionOf(string fileName) =>
        fileName.Length > 1 && fileName[0] == '.' && fileName.IndexOf('.', 1) < 0
            ? ""
            : Path.GetExtension(fileName);
}
