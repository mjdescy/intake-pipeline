using System.Security.Cryptography;
using IntakePipeline.Core.Manifest;
using IntakePipeline.Step.Manifest;

namespace IntakePipeline.Step.Ingest.Tests;

public sealed class IngestRunnerTests
{
    private static readonly Guid _sourceRunId = Guid.Parse("11112222-3333-4444-5555-666677778888");
    private static readonly Guid _runId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");

    [Fact]
    public void Run_CopiesFilesRecursively_DescribingDestinationEntries()
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
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Entries.Count);
        Assert.All(result.Entries, entry => Assert.Null(entry.Error));

        Assert.Equal(rootContent, File.ReadAllBytes(Path.Combine(destinationFolder, "a.txt")));
        Assert.Equal(subContent, File.ReadAllBytes(Path.Combine(destinationFolder, "sub", "b.txt")));
        Assert.Equal(deepContent, File.ReadAllBytes(Path.Combine(destinationFolder, "sub", "deep", "c.bin")));
        Assert.Empty(File.ReadAllBytes(Path.Combine(destinationFolder, "empty.bin")));

        // This step copies; it never moves.
        Assert.True(File.Exists(rootFile));
        Assert.True(File.Exists(subFile));
        Assert.True(File.Exists(deepFile));
        Assert.True(File.Exists(emptyFile));

        ManifestFileEntry rootEntry = EntryFor(result, "a.txt");
        Assert.Equal(Path.Combine(destinationFolder, "a.txt"), rootEntry.FilePath);
        Assert.Equal("a.txt", rootEntry.FileName);
        Assert.Equal(".txt", rootEntry.FileExtension);
        Assert.Equal(ExpectedSha256(rootContent), rootEntry.Sha256);
        Assert.Equal(rootContent.Length, rootEntry.FileSizeInBytes);

        ManifestFileEntry deepEntry = EntryFor(result, "c.bin");
        Assert.Equal(".bin", deepEntry.FileExtension);
        Assert.Equal(ExpectedSha256(deepContent), deepEntry.Sha256);
        Assert.Equal(deepContent.Length, deepEntry.FileSizeInBytes);
        Assert.Equal(0, EntryFor(result, "empty.bin").FileSizeInBytes);

        Assert.Equal(_runId, result.RunId);
        Assert.Equal(destinationFolder, result.Folder);
        Assert.True(result.StartedAtUtc <= result.CompletedAtUtc);
    }

    [Fact]
    public void Run_RecordsProvenance_LinkedToSourceManifest()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), "content"u8.ToArray());
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string parentManifestPath = Path.Combine(directory.FullPath, "source-manifest.json");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, parentManifestPath);

        ManifestProvenance provenance = Assert.IsType<ManifestProvenance>(result.Provenance);
        Assert.Equal(ManifestSteps.Ingest, provenance.Step);
        Assert.Equal(_sourceRunId, provenance.ParentRunId);
        Assert.Equal(parentManifestPath, provenance.ParentManifestPath);
    }

    [Fact]
    public void Run_OutputEntries_PointAtDestinationFiles()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(Path.Combine(sourceFolder, "sub"));
        File.WriteAllBytes(Path.Combine(sourceFolder, "sub", "a.txt"), "content"u8.ToArray());
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        ManifestFileEntry entry = Assert.Single(result.Entries);
        Assert.Equal(Path.Combine(destinationFolder, "sub", "a.txt"), entry.FilePath);
        Assert.StartsWith(destinationFolder, entry.FilePath, StringComparison.Ordinal);
        Assert.DoesNotContain(sourceFolder, entry.FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WithEmptyManifest_RecordsNoEntriesAsSuccess()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Run_CreatesDestinationFolder_WhenMissing()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), [1, 2, 3]);
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.True(Directory.Exists(destinationFolder));
        Assert.True(File.Exists(Path.Combine(destinationFolder, "a.txt")));
    }

    [Fact]
    public void Run_WithDestinationEqualToSource_RecordsGlobalError()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), [1]);
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);

        FileManifest result = IngestRunner.Run(source, sourceFolder, _runId, null);

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
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);

        FileManifest result = IngestRunner.Run(source, Path.Combine(sourceFolder, "inside"), _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Empty(result.Entries);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Run_WithDestinationContainingSource_RecordsGlobalError()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), [1]);
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);

        FileManifest result = IngestRunner.Run(source, directory.FullPath, _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Empty(result.Entries);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Run_WithDuplicateDestinationPaths_RecordsGlobalErrorAndCopiesNothing()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        string filePath = Path.Combine(sourceFolder, "a.txt");
        File.WriteAllBytes(filePath, "content"u8.ToArray());
        FileManifest source = BuildManifest(
            sourceFolder,
            new ManifestFileEntry(filePath, "a.txt", null, null, ".txt", null),
            new ManifestFileEntry(filePath, "a.txt", null, null, ".txt", null));
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Empty(result.Entries);
        Assert.Contains(result.Errors, error => error.Contains("same destination", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(destinationFolder, "a.txt")));
    }

    [Fact]
    public void Run_WithNonAbsoluteEntryPath_RecordsPerEntryError()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        FileManifest source = BuildManifest(
            sourceFolder,
            new ManifestFileEntry("relative/a.txt", "a.txt", null, null, ".txt", null));
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        ManifestFileEntry entry = Assert.Single(result.Entries);
        Assert.NotNull(entry.Error);
        Assert.Contains("not absolute", entry.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WithEntryEscapingSourceFolder_RecordsPerEntryError()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        FileManifest source = BuildManifest(
            sourceFolder,
            new ManifestFileEntry("/elsewhere/x.txt", "x.txt", null, null, ".txt", null));
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        ManifestFileEntry entry = Assert.Single(result.Entries);
        Assert.NotNull(entry.Error);
        Assert.Contains("escapes the manifested folder", entry.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WithUpstreamEntryError_ReEmitsErrorAndStillProcessesOtherEntries()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        byte[] goodContent = "good"u8.ToArray();
        string goodFile = Path.Combine(sourceFolder, "good.txt");
        File.WriteAllBytes(goodFile, goodContent);
        FileManifest source = BuildManifest(
            sourceFolder,
            new ManifestFileEntry(
                Path.Combine(sourceFolder, "bad.txt"), "bad.txt", null, null, ".txt", "upstream boom"),
            new ManifestFileEntry(
                goodFile, "good.txt", ExpectedSha256(goodContent), goodContent.Length, ".txt", null));
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        ManifestFileEntry bad = EntryFor(result, "bad.txt");
        Assert.NotNull(bad.Error);
        Assert.StartsWith("skipped: ", bad.Error, StringComparison.Ordinal);
        Assert.Contains("upstream boom", bad.Error, StringComparison.Ordinal);
        Assert.Null(EntryFor(result, "good.txt").Error);
        Assert.True(File.Exists(Path.Combine(destinationFolder, "good.txt")));
    }

    [Fact]
    public void Run_WhenSourceFileDeletedAfterManifest_RecordsPerEntryErrorAndCopiesOthers()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllBytes(Path.Combine(sourceFolder, "keep.txt"), "keep"u8.ToArray());
        File.WriteAllBytes(Path.Combine(sourceFolder, "gone.txt"), "gone"u8.ToArray());
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);
        File.Delete(Path.Combine(sourceFolder, "gone.txt"));
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Null(EntryFor(result, "keep.txt").Error);
        Assert.NotNull(EntryFor(result, "gone.txt").Error);
        Assert.True(File.Exists(Path.Combine(destinationFolder, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(destinationFolder, "gone.txt")));
    }

    [Fact]
    public void Run_WhenSourceChangedAfterManifest_RecordsHashMismatchError()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        byte[] originalContent = "original"u8.ToArray();
        File.WriteAllBytes(Path.Combine(sourceFolder, "changed.txt"), originalContent);
        File.WriteAllBytes(Path.Combine(sourceFolder, "stable.txt"), "stable"u8.ToArray());
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);
        byte[] changedContent = "changed after manifest"u8.ToArray();
        File.WriteAllBytes(Path.Combine(sourceFolder, "changed.txt"), changedContent);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        ManifestFileEntry changed = EntryFor(result, "changed.txt");
        Assert.NotNull(changed.Error);
        Assert.Contains("does not match the manifest hash", changed.Error, StringComparison.Ordinal);
        Assert.Null(EntryFor(result, "stable.txt").Error);
        Assert.Equal(changedContent, File.ReadAllBytes(Path.Combine(destinationFolder, "changed.txt")));
    }

    [Fact]
    public void Run_WithSymlinkedSourceEntry_RecordsPerEntryError()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        string realFile = Path.Combine(sourceFolder, "real.txt");
        File.WriteAllBytes(realFile, "real"u8.ToArray());
        string linkFile = Path.Combine(sourceFolder, "alias.txt");
        if (!TryCreateFileSymlink(linkFile, realFile))
        {
            // Symlink creation is not permitted on this platform; nothing to assert.
            return;
        }

        FileManifest source = BuildManifest(
            sourceFolder,
            new ManifestFileEntry(linkFile, "alias.txt", null, null, ".txt", null));
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        ManifestFileEntry entry = Assert.Single(result.Entries);
        Assert.NotNull(entry.Error);
        Assert.Contains("symbolic link", entry.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(destinationFolder, "alias.txt")));
    }

    [Fact]
    public void Run_WithSymlinkedDestinationSubdirectory_RecordsPerEntryError()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(Path.Combine(sourceFolder, "sub"));
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), "a"u8.ToArray());
        File.WriteAllBytes(Path.Combine(sourceFolder, "sub", "b.txt"), "b"u8.ToArray());
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        Directory.CreateDirectory(destinationFolder);
        string symlinkTarget = Path.Combine(directory.FullPath, "target");
        Directory.CreateDirectory(symlinkTarget);
        if (!TryCreateDirectorySymlink(Path.Combine(destinationFolder, "sub"), symlinkTarget))
        {
            // Symlink creation is not permitted on this platform; nothing to assert.
            return;
        }

        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Null(EntryFor(result, "a.txt").Error);
        ManifestFileEntry blocked = EntryFor(result, "b.txt");
        Assert.NotNull(blocked.Error);
        Assert.Contains("symbolic link", blocked.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_OverwritesExistingDestinationFiles_WhenReRun()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        byte[] content = "latest content"u8.ToArray();
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), content);
        FileManifest source = ManifestRunner.Run(_sourceRunId, sourceFolder);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        Directory.CreateDirectory(destinationFolder);
        File.WriteAllBytes(Path.Combine(destinationFolder, "a.txt"), "stale content"u8.ToArray());

        FileManifest result = IngestRunner.Run(source, destinationFolder, _runId, null);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Equal(content, File.ReadAllBytes(Path.Combine(destinationFolder, "a.txt")));
    }

    private static FileManifest BuildManifest(string folder, params ManifestFileEntry[] entries) =>
        new(
            Guid.NewGuid(),
            folder,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            ManifestStatus.Success,
            entries,
            []);

    private static bool TryCreateFileSymlink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static bool TryCreateDirectorySymlink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    private static ManifestFileEntry EntryFor(FileManifest result, string fileName) =>
        result.Entries.Single(entry => entry.FileName == fileName);

    private static string ExpectedSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
