using System.Text.Json;
using IntakePipeline.Step.Ingest.Manifest;

namespace IntakePipeline.Step.Ingest.Tests.Manifest;

public sealed class ManifestIOTests
{
    [Fact]
    public void ReadIngestManifest_ResolvesRelativePathsAgainstManifestDirectory()
    {
        using TempDirectory directory = new();
        string manifestPath = TestManifests.Write(directory.FullPath, "source", "dest");

        IngestManifest manifest = ManifestIO.ReadIngestManifest(manifestPath);

        Assert.Equal(Path.GetFullPath(Path.Combine(directory.FullPath, "source")), manifest.SourceFolder);
        Assert.Equal(Path.GetFullPath(Path.Combine(directory.FullPath, "dest")), manifest.DestinationFolder);
    }

    [Fact]
    public void ReadIngestManifest_PassesAbsolutePathsThroughUnchanged()
    {
        using TempDirectory directory = new();
        string absoluteSource = Path.GetFullPath(Path.Combine(directory.FullPath, "source"));
        string absoluteDestination = Path.GetFullPath(Path.Combine(directory.FullPath, "dest"));
        string manifestPath = TestManifests.Write(directory.FullPath, absoluteSource, absoluteDestination);

        IngestManifest manifest = ManifestIO.ReadIngestManifest(manifestPath);

        Assert.Equal(absoluteSource, manifest.SourceFolder);
        Assert.Equal(absoluteDestination, manifest.DestinationFolder);
    }

    [Fact]
    public void ReadIngestManifest_WithMissingFile_ThrowsFileNotFoundException()
    {
        using TempDirectory directory = new();
        string manifestPath = Path.Combine(directory.FullPath, "missing.json");

        Assert.Throws<FileNotFoundException>(() => ManifestIO.ReadIngestManifest(manifestPath));
    }

    [Fact]
    public void ReadIngestManifest_WithInvalidJson_ThrowsInvalidDataException()
    {
        using TempDirectory directory = new();
        string manifestPath = Path.Combine(directory.FullPath, "manifest.json");
        File.WriteAllText(manifestPath, "{ not valid json");

        Assert.Throws<InvalidDataException>(() => ManifestIO.ReadIngestManifest(manifestPath));
    }

    [Fact]
    public void ReadIngestManifest_WithMissingRequiredFields_ThrowsInvalidDataException()
    {
        using TempDirectory directory = new();
        string manifestPath = Path.Combine(directory.FullPath, "manifest.json");
        File.WriteAllText(manifestPath, """{ "sourceFolder": "source" }""");

        Assert.Throws<InvalidDataException>(() => ManifestIO.ReadIngestManifest(manifestPath));
    }

    [Fact]
    public void ReadIngestManifest_WithBlankFieldValues_ThrowsInvalidDataException()
    {
        using TempDirectory directory = new();
        string manifestPath = Path.Combine(directory.FullPath, "manifest.json");
        File.WriteAllText(manifestPath, """{ "sourceFolder": " ", "destinationFolder": "dest" }""");

        Assert.Throws<InvalidDataException>(() => ManifestIO.ReadIngestManifest(manifestPath));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    public void ReadIngestManifest_WithNonObjectJson_ThrowsInvalidDataException(string json)
    {
        using TempDirectory directory = new();
        string manifestPath = Path.Combine(directory.FullPath, "manifest.json");
        File.WriteAllText(manifestPath, json);

        Assert.Throws<InvalidDataException>(() => ManifestIO.ReadIngestManifest(manifestPath));
    }

    [Fact]
    public void WriteIngestResultManifest_CreatesParentDirectoryAndRoundTrips()
    {
        using TempDirectory directory = new();
        IngestResultManifest manifest = new(
            "/source",
            "/dest",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero),
            ManifestStatus.Failure,
            [new IngestResultEntry("/source/a.txt", "/dest/a.txt", "abc123", 12, "boom")],
            ["global error"]);

        string path = Path.Combine(directory.FullPath, "nested", "deep", "result.json");
        ManifestIO.WriteIngestResultManifest(manifest, path);

        Assert.True(File.Exists(path));
        IngestResultManifest readBack = ManifestIO.ReadIngestResultManifest(path);
        Assert.Equal(manifest.SourceFolder, readBack.SourceFolder);
        Assert.Equal(manifest.DestinationFolder, readBack.DestinationFolder);
        Assert.Equal(manifest.StartedAtUtc, readBack.StartedAtUtc);
        Assert.Equal(manifest.CompletedAtUtc, readBack.CompletedAtUtc);
        Assert.Equal(manifest.Status, readBack.Status);
        Assert.Equal(manifest.Entries, readBack.Entries);
        Assert.Equal(manifest.Errors, readBack.Errors);
    }

    [Fact]
    public void SerializeIngestResultManifest_ProducesCamelCaseJson()
    {
        IngestResultManifest manifest = new(
            "/source",
            "/dest",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            ManifestStatus.Success,
            [new IngestResultEntry("/source/a.txt", "/dest/a.txt", "abc123", 12, null)],
            []);

        string json = ManifestIO.SerializeIngestResultManifest(manifest);
        IngestResultManifest? roundTripped = JsonSerializer.Deserialize<IngestResultManifest>(
            json, TestManifests.CamelCase);

        Assert.NotNull(roundTripped);
        Assert.Equal(ManifestStatus.Success, roundTripped.Status);
        IngestResultEntry entry = Assert.Single(roundTripped.Entries);
        Assert.Equal("/source/a.txt", entry.SourceFilePath);
        Assert.Equal("/dest/a.txt", entry.DestinationFilePath);
        Assert.Equal("abc123", entry.Sha256);
        Assert.Equal(12, entry.FileSizeInBytes);
        Assert.Null(entry.Error);
    }
}
