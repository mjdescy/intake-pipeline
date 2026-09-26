using System.Text.Json;
using IntakePipeline.Core.Manifest;
using IntakePipeline.Step.Manifest;

namespace IntakePipeline.Step.Ingest.Tests;

/// <summary>
/// CLI tests that call <see cref="Program.Main"/> directly. They run in the
/// "cli" collection so the process-wide Console redirection they perform
/// never overlaps with another CLI test.
/// </summary>
[Collection("cli")]
public sealed class ProgramTests
{
    private const string ValidRunId = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

    [Fact]
    public void Main_WithValidManifestInQuietMode_WritesResultManifestWithoutStdout()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = ManifestFor(directory);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            manifestPath, "-d", destinationFolder, "--run-id", ValidRunId, "-o", outputPath, "--quiet");

        Assert.Equal(0, exitCode);
        Assert.Empty(stdOut);
        Assert.Empty(stdErr);
        FileManifest result = ManifestIO.ReadFileManifest(outputPath);
        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Equal(Guid.Parse(ValidRunId), result.RunId);
        Assert.Single(result.Entries);
    }

    [Fact]
    public void MainInDefaultMode_ReportsSummaryOnStdout()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = ManifestFor(directory);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            manifestPath, "-d", destinationFolder, "-o", outputPath);

        Assert.Equal(0, exitCode);
        Assert.Contains("Ingested 1 of 1 files", stdOut, StringComparison.Ordinal);
        Assert.Contains(outputPath, stdOut, StringComparison.Ordinal);
        Assert.Empty(stdErr);
    }

    [Fact]
    public void Main_WithJson_PrintsResultManifestToStdout()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = ManifestFor(directory);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            manifestPath, "-d", destinationFolder, "--json");

        Assert.Equal(0, exitCode);
        Assert.Empty(stdErr);
        FileManifest? manifest = JsonSerializer.Deserialize<FileManifest>(stdOut, TestManifests.CamelCase);
        Assert.NotNull(manifest);
        Assert.Equal(ManifestStatus.Success, manifest.Status);
        Assert.Single(manifest.Entries);
        ManifestProvenance provenance = Assert.IsType<ManifestProvenance>(manifest.Provenance);
        Assert.Equal(ManifestSteps.Ingest, provenance.Step);
    }

    [Fact]
    public void Main_WithJsonAndQuiet_StillPrintsJsonToStdout()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = ManifestFor(directory);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        (int exitCode, string stdOut, _) = RunMain(
            manifestPath, "-d", destinationFolder, "--json", "--quiet");

        Assert.Equal(0, exitCode);
        Assert.NotEmpty(stdOut);
    }

    [Fact]
    public void Main_WithoutDestination_FailsWithUsageError()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = ManifestFor(directory);
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath, "-o", outputPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithoutManifestArgument_FailsWithUsageError()
    {
        using TempDirectory directory = new();
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            "-d", destinationFolder, "-o", outputPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithInvalidRunId_FailsWithUsageError()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = ManifestFor(directory);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            manifestPath, "-d", destinationFolder, "--run-id", "not-a-guid", "-o", outputPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithoutOutputOrJson_FailsWithUsageError()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = ManifestFor(directory);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath, "-d", destinationFolder);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithMissingManifestFile_FailsWithUsageError()
    {
        using TempDirectory directory = new();
        string manifestPath = Path.Combine(directory.FullPath, "missing.json");
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            manifestPath, "-d", destinationFolder, "-o", outputPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.Contains(manifestPath, stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_WithOldStyleConfigManifest_FailsWithUsageError()
    {
        using TempDirectory directory = new();
        string manifestPath = Path.Combine(directory.FullPath, "config.json");
        File.WriteAllText(
            manifestPath,
            """{"sourceFolder":"source","destinationFolder":"dest"}""");
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            manifestPath, "-d", destinationFolder, "-o", outputPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.Contains("not a file manifest", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_WithEmptyFolderManifest_SucceedsWithZeroCounts()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        string manifestPath = TestManifests.Write(
            ManifestRunner.Run(Guid.NewGuid(), sourceFolder), directory.FullPath);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            manifestPath, "-d", destinationFolder, "-o", outputPath);

        Assert.Equal(0, exitCode);
        Assert.Contains("Ingested 0 of 0 files", stdOut, StringComparison.Ordinal);
        Assert.Empty(stdErr);
    }

    [Fact]
    public void Main_WithSourceFolderMissingOnDisk_FailsWithRuntimeErrorAndStillWritesManifest()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = ManifestFor(directory);
        Directory.Delete(Path.Combine(directory.FullPath, "source"), recursive: true);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            manifestPath, "-d", destinationFolder, "-o", outputPath);

        Assert.Equal(2, exitCode);
        Assert.NotEmpty(stdErr);
        FileManifest result = ManifestIO.ReadFileManifest(outputPath);
        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.True(
            result.Errors.Count > 0 || result.Entries.Any(entry => entry.Error is not null),
            "Expected the result manifest to record at least one error.");
    }

    [Fact]
    public void Main_WhenResultManifestCannotBeWritten_FailsWithRuntimeError()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = ManifestFor(directory);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        // A directory squatting on the output path makes writing the manifest fail.
        string blockedPath = Path.Combine(directory.FullPath, "blocked");
        Directory.CreateDirectory(blockedPath);

        (int exitCode, string stdOut, string stdErr) = RunMain(
            manifestPath, "-d", destinationFolder, "-o", blockedPath);

        Assert.Equal(2, exitCode);
        Assert.Contains("could not be written", stdErr, StringComparison.Ordinal);
        Assert.Contains("Ingested 1 of 1 files", stdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_WithVersion_PrintsVersionToStdoutAndExitsZero()
    {
        (int exitCode, string stdOut, string stdErr) = RunMain("--version");

        Assert.Equal(0, exitCode);
        Assert.Contains("IntakePipeline.Step.Ingest", stdOut, StringComparison.Ordinal);
        Assert.Empty(stdErr);
    }

    [Fact]
    public void Main_WithHelp_PrintsHelpToStdoutAndExitsZero()
    {
        (int exitCode, string stdOut, string stdErr) = RunMain("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--destination", stdOut, StringComparison.Ordinal);
        Assert.Empty(stdErr);
    }

    [Fact]
    public void Main_WithUnknownOption_FailsWithUsageErrorOnStderr()
    {
        (int exitCode, string stdOut, string stdErr) = RunMain("--bogus");

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithoutArguments_FailsWithUsageError()
    {
        (int exitCode, string stdOut, string stdErr) = RunMain();

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    private static TempDirectory CreatePopulatedSource()
    {
        TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), [1, 2, 3]);
        return directory;
    }

    private static string ManifestFor(TempDirectory directory)
    {
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        return TestManifests.Write(ManifestRunner.Run(Guid.NewGuid(), sourceFolder), directory.FullPath);
    }

    private static (int ExitCode, string StdOut, string StdErr) RunMain(params string[] args)
    {
        using StringWriter stdOut = new();
        using StringWriter stdErr = new();
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        Console.SetOut(stdOut);
        Console.SetError(stdErr);
        try
        {
            int exitCode = Program.Main(args);
            return (exitCode, stdOut.ToString(), stdErr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
