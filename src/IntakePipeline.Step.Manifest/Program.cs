using System.Reflection;
using CommandLine;
using IntakePipeline.Core.Manifest;

namespace IntakePipeline.Step.Manifest;

/// <summary>
/// Console entry point for the manifest step. Thin by design: it parses
/// arguments, delegates to <see cref="ManifestRunner"/>, and honors the CLI
/// output contract (clean text by default, nothing with --quiet, JSON with
/// --json, errors on stderr in every mode).
/// </summary>
public static class Program
{
    /// <summary>Console entry point. Returns the process exit code.</summary>
    public static int Main(string[] args)
    {
        // Buffer the library's auto-generated help text so it can be routed to
        // the right stream: stdout when requested (--help), stderr when the
        // command line itself is broken. --version is handled in Run because
        // the library's auto version reports the entry assembly, which is the
        // test host when the app runs under `dotnet test`.
        StringWriter helpBuffer = new();
        Parser parser = new(with =>
        {
            with.HelpWriter = helpBuffer;
            with.AutoVersion = false;
        });
        return parser.ParseArguments<ManifestOptions>(args)
            .MapResult(Run, errors => HandleParseErrors(errors, helpBuffer));
    }

    private static int HandleParseErrors(IEnumerable<Error> errors, StringWriter helpBuffer)
    {
        if (errors.IsHelp())
        {
            // --help is a meta-action: it wins over --quiet and --json, and it
            // is a success, not a usage error.
            Console.Out.Write(helpBuffer);
            return (int)ExitCode.Success;
        }

        Console.Error.Write(helpBuffer);
        return (int)ExitCode.UsageError;
    }

    private static int Run(ManifestOptions options)
    {
        if (options.Version)
        {
            // --version is a meta-action like --help: it wins over --quiet
            // and --json. The informational version carries the git hash.
            Console.Out.WriteLine($"IntakePipeline.Step.Manifest {InformationalVersion}");
            return (int)ExitCode.Success;
        }

        if (string.IsNullOrWhiteSpace(options.FolderPath))
        {
            Console.Error.WriteLine(
                "No folder given. Pass the path to the folder to manifest; pass --help for full usage.");
            return (int)ExitCode.UsageError;
        }

        if (string.IsNullOrWhiteSpace(options.RunId) || !Guid.TryParse(options.RunId, out Guid runId))
        {
            Console.Error.WriteLine(
                "No valid run id given. Pass --run-id <guid>, e.g. --run-id 6f9619ff-8b86-d011-b42d-00cf4fc964ff.");
            return (int)ExitCode.UsageError;
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath) && !options.Json)
        {
            Console.Error.WriteLine(
                "No output specified. Pass --output <path> to write the manifest, or --json to print it to stdout.");
            return (int)ExitCode.UsageError;
        }

        string folder;
        try
        {
            folder = Path.GetFullPath(options.FolderPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Console.Error.WriteLine($"Folder path is not valid: '{options.FolderPath}' ({ex.Message}).");
            return (int)ExitCode.UsageError;
        }

        FileManifest manifest = ManifestRunner.Run(runId, folder);

        bool manifestWritten = true;
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            try
            {
                ManifestIO.WriteFileManifest(manifest, options.OutputPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException
                or ArgumentException or NotSupportedException or PathTooLongException)
            {
                manifestWritten = false;
                Console.Error.WriteLine($"Manifest could not be written to '{options.OutputPath}': {ex.Message}");
            }
        }

        WriteErrors(manifest);

        if (options.Json)
        {
            // --json wins over --quiet: the JSON payload is the point. Print it
            // even when the manifest file could not be written, so the run's
            // data is not lost.
            Console.Out.WriteLine(ManifestIO.SerializeFileManifest(manifest));
        }
        else if (!options.Quiet)
        {
            int describedCount = manifest.Entries.Count(entry => entry.Error is null);
            int errorCount = manifest.Errors.Count + manifest.Entries.Count(entry => entry.Error is not null);
            Console.Out.WriteLine(
                $"Described {describedCount} of {manifest.Entries.Count} files under '{manifest.Folder}'.");
            if (manifestWritten && !string.IsNullOrWhiteSpace(options.OutputPath))
            {
                Console.Out.WriteLine($"Manifest: {Path.GetFullPath(options.OutputPath)}");
            }
            if (errorCount > 0)
            {
                Console.Out.WriteLine($"{errorCount} error(s); see the manifest for details.");
            }
        }

        return manifest.Status == ManifestStatus.Success && manifestWritten
            ? (int)ExitCode.Success
            : (int)ExitCode.RuntimeError;
    }

    // The informational version, e.g. "0.1.0+49742dd", is the SDK-generated
    // version plus the git hash, so it identifies the exact build.
    private static string InformationalVersion =>
        typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    // The CLI output contract requires error messages on stderr in every mode.
    private static void WriteErrors(FileManifest manifest)
    {
        foreach (string error in manifest.Errors)
        {
            Console.Error.WriteLine(error);
        }

        foreach (string? entryError in manifest.Entries.Select(entry => entry.Error).Where(error => error is not null))
        {
            Console.Error.WriteLine(entryError);
        }
    }
}
