using System.Security.Cryptography;
using IntakePipeline.Core.Manifest;

namespace IntakePipeline.Step.Manifest.Tests;

public sealed class ManifestRunnerTests
{
    [Fact]
    public void Run_DescribesFilesRecursively_WithHashesSizesAndStructure()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(Path.Combine(folder, "sub", "deep"));
        byte[] rootContent = "hello manifest"u8.ToArray();
        byte[] subContent = "nested file"u8.ToArray();
        byte[] deepContent = [0x00, 0x01, 0x02, 0xff, 0xfe];
        Guid runId = Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff");
        File.WriteAllBytes(Path.Combine(folder, "a.txt"), rootContent);
        File.WriteAllBytes(Path.Combine(folder, "sub", "b.txt"), subContent);
        File.WriteAllBytes(Path.Combine(folder, "sub", "deep", "c.bin"), deepContent);
        File.WriteAllBytes(Path.Combine(folder, "empty.bin"), []);

        FileManifest result = ManifestRunner.Run(runId, folder);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Entries.Count);
        Assert.All(result.Entries, entry => Assert.Null(entry.Error));

        Assert.Equal(ExpectedSha256(rootContent), EntryFor(result, "a.txt").Sha256);
        Assert.Equal(rootContent.Length, EntryFor(result, "a.txt").FileSizeInBytes);
        Assert.Equal(ExpectedSha256(subContent), EntryFor(result, "b.txt").Sha256);
        Assert.Equal(subContent.Length, EntryFor(result, "b.txt").FileSizeInBytes);
        Assert.Equal(ExpectedSha256(deepContent), EntryFor(result, "c.bin").Sha256);
        Assert.Equal(deepContent.Length, EntryFor(result, "c.bin").FileSizeInBytes);
        Assert.Equal(0, EntryFor(result, "empty.bin").FileSizeInBytes);

        Assert.Equal("a.txt", EntryFor(result, "a.txt").FileName);
        Assert.Equal(".txt", EntryFor(result, "a.txt").FileExtension);
        Assert.Equal(".txt", EntryFor(result, "b.txt").FileExtension);
        Assert.Equal(".bin", EntryFor(result, "c.bin").FileExtension);

        Assert.Equal(
            result.Entries.Select(entry => entry.FilePath).OrderBy(path => path, StringComparer.Ordinal),
            result.Entries.Select(entry => entry.FilePath));

        Assert.Equal(runId, result.RunId);
        Assert.Equal(folder, result.Folder);
        Assert.True(result.StartedAtUtc <= result.CompletedAtUtc);
    }

    [Fact]
    public void Run_WithEmptyFolder_RecordsNoEntriesAsSuccess()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(folder);

        FileManifest result = ManifestRunner.Run(Guid.NewGuid(), folder);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Run_WithMissingFolder_RecordsGlobalErrorOnly()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "missing");

        FileManifest result = ManifestRunner.Run(Guid.NewGuid(), folder);

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Empty(result.Entries);
        Assert.Contains(result.Errors, error => error.Contains(folder, StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WithFileWithoutExtension_RecordsEmptyExtension()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "README"), "no extension"u8.ToArray());

        FileManifest result = ManifestRunner.Run(Guid.NewGuid(), folder);

        ManifestFileEntry entry = Assert.Single(result.Entries);
        Assert.Equal("README", entry.FileName);
        Assert.Equal("", entry.FileExtension);
    }

    [Fact]
    public void Run_PreservesExtensionAndNameCase()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "Report.TXT"), "case"u8.ToArray());

        FileManifest result = ManifestRunner.Run(Guid.NewGuid(), folder);

        ManifestFileEntry entry = Assert.Single(result.Entries);
        Assert.Equal("Report.TXT", entry.FileName);
        Assert.Equal(".TXT", entry.FileExtension);
    }

    [Fact]
    public void Run_EchoesRunIdExactly()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(folder);
        Guid runId = Guid.Parse("11112222-3333-4444-5555-666677778888");

        FileManifest result = ManifestRunner.Run(runId, folder);

        Assert.Equal(runId, result.RunId);
    }

    [Fact]
    public void Run_DoesNotFollowSymlinkedDirectory()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "real.txt"), "real"u8.ToArray());
        string outside = Path.Combine(directory.FullPath, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "secret.txt"), "secret"u8.ToArray());
        if (!TryCreateDirectorySymlink(Path.Combine(folder, "link"), outside))
        {
            // Symlink creation is not permitted on this platform; nothing to assert.
            return;
        }

        FileManifest result = ManifestRunner.Run(Guid.NewGuid(), folder);

        Assert.Equal(ManifestStatus.Success, result.Status);
        ManifestFileEntry entry = Assert.Single(result.Entries);
        Assert.Equal("real.txt", entry.FileName);
        Assert.DoesNotContain(result.Entries, e => e.FileName == "secret.txt");
    }

    [Fact]
    public void Run_SkipsSymlinkedFile()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(folder);
        string realFile = Path.Combine(folder, "real.txt");
        File.WriteAllBytes(realFile, "real"u8.ToArray());
        if (!TryCreateFileSymlink(Path.Combine(folder, "alias.txt"), realFile))
        {
            // Symlink creation is not permitted on this platform; nothing to assert.
            return;
        }

        FileManifest result = ManifestRunner.Run(Guid.NewGuid(), folder);

        Assert.Equal(ManifestStatus.Success, result.Status);
        ManifestFileEntry entry = Assert.Single(result.Entries);
        Assert.Equal("real.txt", entry.FileName);
    }

    [Fact]
    public void Run_WithDotfile_ReportsEmptyExtension()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, ".gitignore"), "ignored"u8.ToArray());
        File.WriteAllBytes(Path.Combine(folder, "a.txt"), "text"u8.ToArray());
        File.WriteAllBytes(Path.Combine(folder, "archive.tar.gz"), "gzip"u8.ToArray());
        File.WriteAllBytes(Path.Combine(folder, "foo."), "trailing dot"u8.ToArray());

        FileManifest result = ManifestRunner.Run(Guid.NewGuid(), folder);

        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Equal("", EntryFor(result, ".gitignore").FileExtension);
        Assert.Equal(".txt", EntryFor(result, "a.txt").FileExtension);
        Assert.Equal(".gz", EntryFor(result, "archive.tar.gz").FileExtension);
        Assert.Equal("", EntryFor(result, "foo.").FileExtension);
    }

    [Fact]
    public void Run_WithUnreadableSubfolder_RecordsGlobalErrorAndStillDescribesReadableFiles()
    {
        if (OperatingSystem.IsWindows())
        {
            // Unix permission bits are not available on Windows; nothing to assert.
            return;
        }

        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        string locked = Path.Combine(folder, "locked");
        Directory.CreateDirectory(locked);
        File.WriteAllBytes(Path.Combine(folder, "readable.txt"), "readable"u8.ToArray());
        File.WriteAllBytes(Path.Combine(locked, "hidden.txt"), "hidden"u8.ToArray());

        FileManifest result;
        try
        {
            File.SetUnixFileMode(locked, UnixFileMode.None);
            if (CanEnumerate(locked))
            {
                // Running with privileges that bypass permission bits (e.g. root);
                // an unreadable folder cannot be simulated here.
                return;
            }

            result = ManifestRunner.Run(Guid.NewGuid(), folder);
        }
        finally
        {
            File.SetUnixFileMode(
                locked,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.Contains(result.Errors, error => error.Contains(locked, StringComparison.Ordinal));
        Assert.Contains(result.Entries, entry => entry.FileName == "readable.txt");
        Assert.DoesNotContain(result.Entries, entry => entry.FileName == "hidden.txt");
    }

    private static bool CanEnumerate(string directory)
    {
        try
        {
            _ = Directory.GetFileSystemEntries(directory);
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

    private static ManifestFileEntry EntryFor(FileManifest result, string fileName) =>
        result.Entries.Single(entry => entry.FileName == fileName);

    private static string ExpectedSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}
