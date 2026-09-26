using System.Text.Json;
using IntakePipeline.Core.Manifest;

namespace IntakePipeline.Core.Tests.Manifest;

public sealed class ManifestIOTests
{
    [Fact]
    public void WriteFileManifest_CreatesParentDirectoryAndRoundTrips()
    {
        using TempDirectory directory = new();
        Guid runId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
        FileManifest manifest = new(
            runId,
            "/folder",
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 2, 3, 4, 6, TimeSpan.Zero),
            ManifestStatus.Failure,
            [new ManifestFileEntry("/folder/a.txt", "a.txt", "abc123", 12, ".txt", "boom")],
            ["global error"]);

        string path = Path.Combine(directory.FullPath, "nested", "deep", "manifest.json");
        ManifestIO.WriteFileManifest(manifest, path);

        Assert.True(File.Exists(path));
        FileManifest readBack = ManifestIO.ReadFileManifest(path);
        Assert.Equal(manifest.RunId, readBack.RunId);
        Assert.Equal(manifest.Folder, readBack.Folder);
        Assert.Equal(manifest.StartedAtUtc, readBack.StartedAtUtc);
        Assert.Equal(manifest.CompletedAtUtc, readBack.CompletedAtUtc);
        Assert.Equal(manifest.Status, readBack.Status);
        Assert.Equal(manifest.Entries, readBack.Entries);
        Assert.Equal(manifest.Errors, readBack.Errors);
    }

    [Fact]
    public void SerializeFileManifest_ProducesCamelCaseJson()
    {
        Guid runId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
        FileManifest manifest = new(
            runId,
            "/folder",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            ManifestStatus.Success,
            [new ManifestFileEntry("/folder/a.txt", "a.txt", "abc123", 12, ".txt", null)],
            []);

        string json = ManifestIO.SerializeFileManifest(manifest);
        FileManifest? roundTripped = JsonSerializer.Deserialize<FileManifest>(
            json, TestJson.CamelCase);

        Assert.NotNull(roundTripped);
        Assert.Equal(ManifestStatus.Success, roundTripped.Status);
        ManifestFileEntry entry = Assert.Single(roundTripped.Entries);
        Assert.Equal("/folder/a.txt", entry.FilePath);
        Assert.Equal("a.txt", entry.FileName);
        Assert.Equal("abc123", entry.Sha256);
        Assert.Equal(12, entry.FileSizeInBytes);
        Assert.Equal(".txt", entry.FileExtension);
        Assert.Null(entry.Error);
    }

    [Fact]
    public void SerializeFileManifest_WithProvenance_RoundTripsAllProvenanceFields()
    {
        Guid parentRunId = Guid.Parse("11112222-3333-4444-5555-666677778888");
        FileManifest manifest = new(
            Guid.NewGuid(),
            "/folder",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            ManifestStatus.Success,
            [],
            [],
            new ManifestProvenance(ManifestSteps.Ingest, parentRunId, "/abs/parent.json"));

        string json = ManifestIO.SerializeFileManifest(manifest);
        FileManifest? roundTripped = JsonSerializer.Deserialize<FileManifest>(
            json, TestJson.CamelCase);

        Assert.NotNull(roundTripped);
        ManifestProvenance provenance = Assert.IsType<ManifestProvenance>(roundTripped.Provenance);
        Assert.Equal(ManifestSteps.Ingest, provenance.Step);
        Assert.Equal(parentRunId, provenance.ParentRunId);
        Assert.Equal("/abs/parent.json", provenance.ParentManifestPath);
    }

    [Fact]
    public void SerializeFileManifest_WithoutProvenance_RoundTripsNullProvenance()
    {
        FileManifest manifest = new(
            Guid.NewGuid(),
            "/folder",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            ManifestStatus.Success,
            [],
            []);

        string json = ManifestIO.SerializeFileManifest(manifest);
        FileManifest? roundTripped = JsonSerializer.Deserialize<FileManifest>(
            json, TestJson.CamelCase);

        Assert.NotNull(roundTripped);
        Assert.Null(roundTripped.Provenance);
    }

    [Fact]
    public void ReadFileManifest_WithMissingFile_ThrowsFileNotFoundException()
    {
        using TempDirectory directory = new();
        string path = Path.Combine(directory.FullPath, "missing.json");

        Assert.Throws<FileNotFoundException>(() => ManifestIO.ReadFileManifest(path));
    }

    [Fact]
    public void ReadFileManifest_WithInvalidJson_ThrowsInvalidDataException()
    {
        using TempDirectory directory = new();
        string path = Path.Combine(directory.FullPath, "manifest.json");
        File.WriteAllText(path, "{ not valid json");

        Assert.Throws<InvalidDataException>(() => ManifestIO.ReadFileManifest(path));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    public void ReadFileManifest_WithNonObjectJson_ThrowsInvalidDataException(string json)
    {
        using TempDirectory directory = new();
        string path = Path.Combine(directory.FullPath, "manifest.json");
        File.WriteAllText(path, json);

        Assert.Throws<InvalidDataException>(() => ManifestIO.ReadFileManifest(path));
    }

    [Fact]
    public void SerializeFileManifest_WithNullManifest_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ManifestIO.SerializeFileManifest(null!));
    }

    [Fact]
    public void WriteFileManifest_WithNullManifest_ThrowsArgumentNullException()
    {
        using TempDirectory directory = new();
        string path = Path.Combine(directory.FullPath, "manifest.json");

        Assert.Throws<ArgumentNullException>(() => ManifestIO.WriteFileManifest(null!, path));
    }

    [Fact]
    public void ReadFileManifest_WithBlankPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ManifestIO.ReadFileManifest(" "));
    }

    [Fact]
    public void WriteFileManifest_WhenPathIsADirectory_Throws()
    {
        using TempDirectory directory = new();
        FileManifest manifest = new(
            Guid.NewGuid(),
            "/folder",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            ManifestStatus.Success,
            [],
            []);
        // A directory squatting on the target path makes both the temp write
        // and the move fail; the implementation must surface that failure.
        string parent = Path.Combine(directory.FullPath, "parent");
        Directory.CreateDirectory(parent);
        string blockedPath = Path.Combine(parent, "blocked");
        Directory.CreateDirectory(blockedPath);

        Exception? exception = Record.Exception(() => ManifestIO.WriteFileManifest(manifest, blockedPath));

        Assert.NotNull(exception);
        Assert.True(
            exception is IOException or UnauthorizedAccessException,
            $"Expected IOException or UnauthorizedAccessException but got {exception.GetType()}.");
        Assert.Empty(Directory.GetFiles(parent, "*.tmp"));
    }
}
