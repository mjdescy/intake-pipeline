using System.Text.Json;
using IntakePipeline.Step.Manifest.Manifest;

namespace IntakePipeline.Step.Manifest.Tests;

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
    public void Main_WithValidFolderInQuietMode_WritesManifestWithoutStdout()
    {
        using TempDirectory directory = CreatePopulatedFolder();
        string folder = Path.Combine(directory.FullPath, "source");
        string outputPath = Path.Combine(directory.FullPath, "manifest.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            folder, "--run-id", ValidRunId, "--output", outputPath, "--quiet");

        Assert.Equal(0, exitCode);
        Assert.Empty(stdOut);
        Assert.Empty(stdErr);
        FileManifest manifest = ManifestIO.ReadFileManifest(outputPath);
        Assert.Equal(ManifestStatus.Success, manifest.Status);
        Assert.Equal(Guid.Parse(ValidRunId), manifest.RunId);
        Assert.Single(manifest.Entries);
    }

    [Fact]
    public void MainInDefaultMode_ReportsSummaryOnStdout()
    {
        using TempDirectory directory = CreatePopulatedFolder();
        string folder = Path.Combine(directory.FullPath, "source");
        string outputPath = Path.Combine(directory.FullPath, "manifest.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            folder, "--run-id", ValidRunId, "--output", outputPath);

        Assert.Equal(0, exitCode);
        Assert.Contains("Described 1 of 1 files", stdOut, StringComparison.Ordinal);
        Assert.Contains(outputPath, stdOut, StringComparison.Ordinal);
        Assert.Empty(stdErr);
    }

    [Fact]
    public void Main_WithJson_PrintsManifestToStdout()
    {
        using TempDirectory directory = CreatePopulatedFolder();
        string folder = Path.Combine(directory.FullPath, "source");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            folder, "--run-id", ValidRunId, "--json");

        Assert.Equal(0, exitCode);
        Assert.Empty(stdErr);
        FileManifest? manifest = JsonSerializer.Deserialize<FileManifest>(stdOut, TestJson.CamelCase);
        Assert.NotNull(manifest);
        Assert.Equal(ManifestStatus.Success, manifest.Status);
        Assert.Single(manifest.Entries);
    }

    [Fact]
    public void Main_WithJsonAndQuiet_StillPrintsJsonToStdout()
    {
        using TempDirectory directory = CreatePopulatedFolder();
        string folder = Path.Combine(directory.FullPath, "source");

        (int exitCode, string stdOut, _) = RunMain(
            folder, "--run-id", ValidRunId, "--json", "--quiet");

        Assert.Equal(0, exitCode);
        Assert.NotEmpty(stdOut);
    }

    [Fact]
    public void Main_WithoutOutputOrJson_FailsWithUsageError()
    {
        using TempDirectory directory = CreatePopulatedFolder();
        string folder = Path.Combine(directory.FullPath, "source");

        (int exitCode, string stdOut, string stdErr) = RunMain(folder, "--run-id", ValidRunId);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithoutRunId_FailsWithUsageError()
    {
        using TempDirectory directory = CreatePopulatedFolder();
        string folder = Path.Combine(directory.FullPath, "source");
        string outputPath = Path.Combine(directory.FullPath, "manifest.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(folder, "--output", outputPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithMalformedRunId_FailsWithUsageError()
    {
        using TempDirectory directory = CreatePopulatedFolder();
        string folder = Path.Combine(directory.FullPath, "source");
        string outputPath = Path.Combine(directory.FullPath, "manifest.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            folder, "--run-id", "not-a-guid", "--output", outputPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithoutFolderArgument_FailsWithUsageError()
    {
        using TempDirectory directory = new();
        string outputPath = Path.Combine(directory.FullPath, "manifest.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            "--run-id", ValidRunId, "--output", outputPath);

        Assert.Equal(1, exitCode);
        Assert.Empty(stdOut);
        Assert.NotEmpty(stdErr);
    }

    [Fact]
    public void Main_WithMissingFolder_FailsWithRuntimeErrorAndStillWritesManifest()
    {
        using TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "missing");
        string outputPath = Path.Combine(directory.FullPath, "manifest.json");

        (int exitCode, string stdOut, string stdErr) = RunMain(
            folder, "--run-id", ValidRunId, "--output", outputPath);

        Assert.Equal(2, exitCode);
        Assert.NotEmpty(stdErr);
        FileManifest manifest = ManifestIO.ReadFileManifest(outputPath);
        Assert.Equal(ManifestStatus.Failure, manifest.Status);
        Assert.NotEmpty(manifest.Errors);
    }

    [Fact]
    public void Main_WhenManifestCannotBeWritten_FailsWithRuntimeError()
    {
        using TempDirectory directory = CreatePopulatedFolder();
        string folder = Path.Combine(directory.FullPath, "source");
        // A directory squatting on the output path makes writing the manifest fail.
        string blockedPath = Path.Combine(directory.FullPath, "blocked");
        Directory.CreateDirectory(blockedPath);

        (int exitCode, string stdOut, string stdErr) = RunMain(
            folder, "--run-id", ValidRunId, "--output", blockedPath);

        Assert.Equal(2, exitCode);
        Assert.Contains("could not be written", stdErr, StringComparison.Ordinal);
        Assert.Contains("Described 1 of 1 files", stdOut, StringComparison.Ordinal);
    }

    [Fact]
    public void Main_WhenManifestCannotBeWrittenInJsonMode_StillPrintsJsonToStdout()
    {
        using TempDirectory directory = CreatePopulatedFolder();
        string folder = Path.Combine(directory.FullPath, "source");
        string blockedPath = Path.Combine(directory.FullPath, "blocked");
        Directory.CreateDirectory(blockedPath);

        (int exitCode, string stdOut, string stdErr) = RunMain(
            folder, "--run-id", ValidRunId, "--output", blockedPath, "--json");

        Assert.Equal(2, exitCode);
        Assert.Contains("could not be written", stdErr, StringComparison.Ordinal);
        FileManifest? manifest = JsonSerializer.Deserialize<FileManifest>(stdOut, TestJson.CamelCase);
        Assert.NotNull(manifest);
        Assert.Equal(ManifestStatus.Success, manifest.Status);
    }

    [Fact]
    public void Main_WithVersion_PrintsVersionToStdoutAndExitsZero()
    {
        (int exitCode, string stdOut, string stdErr) = RunMain("--version");

        Assert.Equal(0, exitCode);
        Assert.Contains("IntakePipeline.Step.Manifest", stdOut, StringComparison.Ordinal);
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

    private static TempDirectory CreatePopulatedFolder()
    {
        TempDirectory directory = new();
        string folder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "a.txt"), [1, 2, 3]);
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
