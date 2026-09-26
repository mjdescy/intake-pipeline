using System.Text.Encodings.Web;
using System.Text.Json;

namespace IntakePipeline.Core.Manifest;

/// <summary>
/// Reads and writes file manifests as camelCase JSON. Manifests are written
/// indented because they are read by humans and machines alike.
/// </summary>
public static class ManifestIO
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // The relaxed encoder keeps apostrophes and other common characters
        // readable in error messages; the manifests are data files, not HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Reads a file manifest previously written by a pipeline step.
    /// </summary>
    /// <exception cref="FileNotFoundException">The manifest file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file is not a valid file manifest.</exception>
    public static FileManifest ReadFileManifest(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File manifest not found: '{path}'.", path);
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<FileManifest>(stream, _serializerOptions)
                ?? throw new InvalidDataException(
                    $"File manifest '{path}' must be a JSON object with file manifest properties.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"File manifest '{path}' is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Serializes a file manifest as a JSON string.
    /// </summary>
    public static string SerializeFileManifest(FileManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, _serializerOptions);
    }

    /// <summary>
    /// Writes a file manifest to <paramref name="path"/>, creating parent
    /// directories as needed so the manifest can live anywhere.
    /// </summary>
    public static void WriteFileManifest(FileManifest manifest, string path)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        string? parentDirectory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(parentDirectory))
        {
            // CreateDirectory is a no-op when the directory already exists.
            Directory.CreateDirectory(parentDirectory);
        }

        // Write to a sibling temp file and move it into place, so an
        // interrupted run never leaves a truncated manifest behind.
        string tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, SerializeFileManifest(manifest));
            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort only: the original failure is what matters.
        }
    }
}
