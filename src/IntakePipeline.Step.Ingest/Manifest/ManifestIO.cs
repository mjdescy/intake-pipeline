using System.Text.Encodings.Web;
using System.Text.Json;

namespace IntakePipeline.Step.Ingest.Manifest;

/// <summary>
/// Reads and writes ingest manifests as camelCase JSON. Manifests are written
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
    /// Reads an input manifest from <paramref name="path"/>. Relative folder
    /// paths inside the manifest resolve against the manifest file's directory
    /// so a manifest can travel with its folders; absolute paths pass through
    /// unchanged.
    /// </summary>
    /// <exception cref="FileNotFoundException">The manifest file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file is unreadable, not valid JSON, or missing required fields.</exception>
    public static IngestManifest ReadIngestManifest(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Input manifest not found: '{path}'.", path);
        }

        IngestManifest? manifest;
        try
        {
            using FileStream stream = File.OpenRead(path);
            manifest = JsonSerializer.Deserialize<IngestManifest>(stream, _serializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Input manifest '{path}' is not valid JSON: {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new InvalidDataException(
                $"Input manifest '{path}' must be a JSON object with 'sourceFolder' and 'destinationFolder' properties.");
        }

        if (string.IsNullOrWhiteSpace(manifest.SourceFolder) || string.IsNullOrWhiteSpace(manifest.DestinationFolder))
        {
            throw new InvalidDataException(
                $"Input manifest '{path}' must specify both 'sourceFolder' and 'destinationFolder' with non-empty values.");
        }

        string manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        return new IngestManifest(
            ResolvePath(manifest.SourceFolder, manifestDirectory),
            ResolvePath(manifest.DestinationFolder, manifestDirectory));
    }

    /// <summary>
    /// Reads back a result manifest previously written by this step.
    /// </summary>
    /// <exception cref="FileNotFoundException">The manifest file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file is not a valid result manifest.</exception>
    public static IngestResultManifest ReadIngestResultManifest(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Result manifest not found: '{path}'.", path);
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<IngestResultManifest>(stream, _serializerOptions)
                ?? throw new InvalidDataException(
                    $"Result manifest '{path}' must be a JSON object with result manifest properties.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Result manifest '{path}' is not valid JSON: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Serializes a result manifest as a JSON string.
    /// </summary>
    public static string SerializeIngestResultManifest(IngestResultManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, _serializerOptions);
    }

    /// <summary>
    /// Writes a result manifest to <paramref name="path"/>, creating parent
    /// directories as needed so the manifest can live anywhere.
    /// </summary>
    public static void WriteIngestResultManifest(IngestResultManifest manifest, string path)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? parentDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(parentDirectory))
        {
            // CreateDirectory is a no-op when the directory already exists.
            Directory.CreateDirectory(parentDirectory);
        }

        File.WriteAllText(path, SerializeIngestResultManifest(manifest));
    }

    /// <summary>
    /// Resolves a folder path from the manifest against the manifest's own
    /// directory, so manifests can use relative paths.
    /// </summary>
    private static string ResolvePath(string folderPath, string manifestDirectory) =>
        Path.IsPathRooted(folderPath)
            ? Path.GetFullPath(folderPath)
            : Path.GetFullPath(Path.Combine(manifestDirectory, folderPath));
}
