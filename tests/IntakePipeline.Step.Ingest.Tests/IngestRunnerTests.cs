using System.Security.Cryptography;
using IntakePipeline.Step.Ingest.Manifest;

namespace IntakePipeline.Step.Ingest.Tests;

public sealed class IngestRunnerTests
{
    [Fact]
    public void Run_CopiesFilesRecursively_WithHashesSizesAndStructure()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(Path.Combine(sourceFolder, "sub", "deep"));
        byte[] rootContent = "hello intake"u8.ToArray();
        byte[] subContent = "nested file"u8.ToArray();
        byte[] deepContent = [0x00, 0x01, 0x02, 0xff, 0xfe];
        string rootFile = Path.Combine(sourceFolder, "a.txt");
        string subFile = Path.Combine(sourceFolder, "sub", "b.txt");
        string deepFile = Path.Combine(sourceFolder, "sub", "deep", "c.bin");
        string emptyFile = Path.Combine(sourceFolder, "empty.bin");
        File.WriteAllBytes(rootFile, rootContent);
        File.WriteAllBytes(subFile, subContent);
        File.WriteAllBytes(deepFile, deepContent);
        File.WriteAllBytes(emptyFile, []);

        IngestManifest manifest = ManifestIO.ReadIngestManifest(
            TestManifests.Write(directory.FullPath, "source", "dest"));
        IngestResultManifest result = IngestRunner.Run(manifest);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Entries.Count);
        Assert.All(result.Entries, entry => Assert.Null(entry.Error));

        Assert.Equal(rootContent, File.ReadAllBytes(Path.Combine(directory.FullPath, "dest", "a.txt")));
        Assert.Equal(subContent, File.ReadAllBytes(Path.Combine(directory.FullPath, "dest", "sub", "b.txt")));
        Assert.Equal(deepContent, File.ReadAllBytes(Path.Combine(directory.FullPath, "dest", "sub", "deep", "c.bin")));
        Assert.Empty(File.ReadAllBytes(Path.Combine(directory.FullPath, "dest", "empty.bin")));

        // This step copies; it never moves.
        Assert.True(File.Exists(rootFile));

        Assert.Equal(ExpectedSha256(rootContent), EntryFor(result, "a.txt").Sha256);
        Assert.Equal(rootContent.Length, EntryFor(result, "a.txt").FileSizeInBytes);
        Assert.Equal(ExpectedSha256(subContent), EntryFor(result, "b.txt").Sha256);
        Assert.Equal(ExpectedSha256(deepContent), EntryFor(result, "c.bin").Sha256);
        Assert.Equal(0, EntryFor(result, "empty.bin").FileSizeInBytes);
        Assert.StartsWith(sourceFolder, result.Entries[0].SourceFilePath, StringComparison.Ordinal);
        Assert.StartsWith(Path.Combine(directory.FullPath, "dest"), result.Entries[0].DestinationFilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WithEmptySourceFolder_RecordsNoEntriesAsSuccess()
    {
        using TempDirectory directory = new();
        Directory.CreateDirectory(Path.Combine(directory.FullPath, "source"));

        IngestManifest manifest = ManifestIO.ReadIngestManifest(
            TestManifests.Write(directory.FullPath, "source", "dest"));
        IngestResultManifest result = IngestRunner.Run(manifest);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Run_WithMissingSourceFolder_RecordsGlobalErrorOnly()
    {
        using TempDirectory directory = new();
        IngestManifest manifest = new(
            Path.Combine(directory.FullPath, "source"),
            Path.Combine(directory.FullPath, "dest"));

        IngestResultManifest result = IngestRunner.Run(manifest);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Empty(result.Entries);
        Assert.Contains(result.Errors, error => error.Contains("source", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_CreatesDestinationFolder_WhenMissing()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), [1, 2, 3]);

        IngestManifest manifest = new(sourceFolder, Path.Combine(directory.FullPath, "dest"));
        IngestResultManifest result = IngestRunner.Run(manifest);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.True(File.Exists(Path.Combine(directory.FullPath, "dest", "a.txt")));
    }

    [Fact]
    public void Run_WithDestinationEqualToSource_RecordsGlobalError()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);

        IngestManifest manifest = new(sourceFolder, sourceFolder);
        IngestResultManifest result = IngestRunner.Run(manifest);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Empty(result.Entries);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Run_WithDestinationInsideSource_RecordsGlobalError()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), [1]);

        IngestManifest manifest = new(sourceFolder, Path.Combine(sourceFolder, "inside"));
        IngestResultManifest result = IngestRunner.Run(manifest);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Empty(result.Entries);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Run_WithBlockedDestinationSubdirectory_RecordsPerFileErrorAndContinues()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(Path.Combine(sourceFolder, "sub"));
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), [1]);
        File.WriteAllBytes(Path.Combine(sourceFolder, "sub", "b.txt"), [2]);

        // A file squatting on the destination subdirectory path makes copying
        // every file under sub/ fail, while a.txt still ingests.
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        Directory.CreateDirectory(destinationFolder);
        File.WriteAllBytes(Path.Combine(destinationFolder, "sub"), [0]);

        IngestManifest manifest = new(sourceFolder, destinationFolder);
        IngestResultManifest result = IngestRunner.Run(manifest);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Equal(2, result.Entries.Count);
        Assert.Null(EntryFor(result, "a.txt").Error);
        Assert.NotNull(EntryFor(result, "b.txt").Error);
        Assert.True(File.Exists(Path.Combine(destinationFolder, "a.txt")));
    }

    [Fact]
    public void Run_OverwritesExistingDestinationFiles_WhenReRun()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        byte[] content = "latest content"u8.ToArray();
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), content);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        Directory.CreateDirectory(destinationFolder);
        File.WriteAllBytes(Path.Combine(destinationFolder, "a.txt"), "stale content"u8.ToArray());

        IngestManifest manifest = new(sourceFolder, destinationFolder);
        IngestResultManifest result = IngestRunner.Run(manifest);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Equal(content, File.ReadAllBytes(Path.Combine(destinationFolder, "a.txt")));
    }

    private static IngestResultEntry EntryFor(IngestResultManifest result, string fileName) =>
        result.Entries.Single(entry => Path.GetFileName(entry.SourceFilePath) == fileName);

    private static string ExpectedSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
