using System.Text.Json;
using IntakePipeline.Step.Ingest.Manifest;

namespace IntakePipeline.Step.Ingest.Tests;

/// <summary>
/// CLI tests that call <see cref="Program.Main"/> directly. They run in the
/// "cli" collection so the process-wide Console redirection they perform
/// never overlaps with another CLI test.
/// </summary>
[Collection("cli")]
public sealed class ProgramTests
{
    [Fact]
    public void Main_WithValidManifestInQuietMode_WritesResultManifestWithoutStdout()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = TestManifests.Write(directory.FullPath, "source", "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath, "--output", outputPath, "--quiet");

        Assert.Equal(0, exitCode);
        Assert.Empty(stdOut);
        Assert.Empty(stdErr);
        IngestResultManifest result = ManifestIO.ReadIngestResultManifest(outputPath);
        Assert.Equal(ManifestStatus.Success, result.Status);
        Assert.Single(result.Entries);
    }

    [Fact]
    public void MainInDefaultMode_ReportsSummaryOnStdout()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = TestManifests.Write(directory.FullPath, "source", "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath, "--output", outputPath);

        Assert.Equal(0, exitCode);
        Assert.Contains("Copied 1 of 1 files", stdOut, StringComparison.Ordinal);
        Assert.Contains(outputPath, stdOut, StringComparison.Ordinal);
        Assert.Empty(stdErr);
    }

    [Fact]
    public void Main_WithJson_PrintsResultManifestToStdout()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = TestManifests.Write(directory.FullPath, "source", "dest");

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath, "--json");

        Assert.Equal(0, exitCode);
        Assert.Empty(stdErr);
        IngestResultManifest? manifest = JsonSerializer.Deserialize<IngestResultManifest>(stdOut, TestManifests.CamelCase);
        Assert.NotNull(manifest);
        Assert.Equal(ManifestStatus.Success, manifest.Status);
        Assert.Single(manifest.Entries);
    }

    [Fact]
    public void Main_WithJsonAndQuiet_StillPrintsJsonToStdout()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = TestManifests.Write(directory.FullPath, "source", "dest");

        (int exitCode, string stdOut, _) = RunMain(manifestPath, "--json", "--quiet");

        Assert.Equal(0, exitCode);
        Assert.NotEmpty(stdOut);
    }

    [Fact]
    public void Main_WithoutOutputOrJson_FailsWithUsageError()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = TestManifests.Write(directory.FullPath, "source", "dest");

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithMissingManifestFile_FailsWithUsageError()
    {
        using TempDirectory directory = new();
        string manifestPath = Path.Combine(directory.FullPath, "missing.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath, "--output", Path.Combine(directory.FullPath, "out.json"));

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.Contains(manifestPath, stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_WithMissingSourceFolder_FailsWithRuntimeErrorAndStillWritesManifest()
    {
        using TempDirectory directory = new();
        string manifestPath = TestManifests.Write(directory.FullPath, "source", "dest");
        string outputPath = Path.Combine(directory.FullPath, "result.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath, "--output", outputPath);

        Assert.Equal(2, exitCode);
        Assert.NotEmpty(stdErr);
        IngestResultManifest result = ManifestIO.ReadIngestResultManifest(outputPath);
        Assert.Equal(ManifestStatus.Failure, result.Status);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void Main_WhenResultManifestCannotBeWritten_FailsWithRuntimeError()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = TestManifests.Write(directory.FullPath, "source", "dest");
        // A directory squatting on the output path makes writing the manifest fail.
        string blockedPath = Path.Combine(directory.FullPath, "blocked");
        Directory.CreateDirectory(blockedPath);

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath, "--output", blockedPath);

        Assert.Equal(2, exitCode);
        Assert.Contains("could not be written", stdErr, StringComparison.Ordinal);
        Assert.Contains("Copied 1 of 1 files", stdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("Result manifest:", stdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_WhenResultManifestCannotBeWrittenInJsonMode_StillPrintsJsonToStdout()
    {
        using TempDirectory directory = CreatePopulatedSource();
        string manifestPath = TestManifests.Write(directory.FullPath, "source", "dest");
        string blockedPath = Path.Combine(directory.FullPath, "blocked");
        Directory.CreateDirectory(blockedPath);

        (int exitCode, string stdOut, string stdErr) = RunMain(manifestPath, "--output", blockedPath, "--json");

        Assert.Equal(2, exitCode);
        Assert.Contains("could not be written", stdErr, StringComparison.Ordinal);
        IngestResultManifest? manifest = JsonSerializer.Deserialize<IngestResultManifest>(stdOut, TestManifests.CamelCase);
        Assert.NotNull(manifest);
        Assert.Equal(ManifestStatus.Success, manifest.Status);
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
        Assert.Contains("--output", stdOut, StringComparison.Ordinal);
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
