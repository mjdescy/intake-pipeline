using System.Reflection;
using CommandLine;
using IntakePipeline.Step.Ingest.Manifest;

namespace IntakePipeline.Step.Ingest;

/// <summary>
/// Console entry point for the ingest step. Thin by design: it parses
/// arguments, delegates to <see cref="IngestRunner"/>, and honors the CLI
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
        return parser.ParseArguments<IngestOptions>(args)
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

    private static int Run(IngestOptions options)
    {
        if (options.Version)
        {
            // --version is a meta-action like --help: it wins over --quiet
            // and --json. The informational version carries the git hash.
            Console.Out.WriteLine($"IntakePipeline.Step.Ingest {InformationalVersion}");
            return (int)ExitCode.Success;
        }

        if (string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            Console.Error.WriteLine(
                "No input manifest given. Pass the path to an input manifest JSON file; pass --help for full usage.");
            return (int)ExitCode.UsageError;
        }

        IngestManifest manifest;
        try
        {
            manifest = ManifestIO.ReadIngestManifest(options.ManifestPath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException
            or UnauthorizedAccessException or IOException)
        {
            Console.Error.WriteLine($"{ex.Message} Check that the manifest is a readable JSON file with 'sourceFolder' and 'destinationFolder'.");
            return (int)ExitCode.UsageError;
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath) && !options.Json)
        {
            Console.Error.WriteLine(
                "No output specified. Pass --output <path> to write the result manifest, or --json to print it to stdout.");
            return (int)ExitCode.UsageError;
        }

        IngestResultManifest result = IngestRunner.Run(manifest);

        bool resultManifestWritten = true;
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            try
            {
                ManifestIO.WriteIngestResultManifest(result, options.OutputPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                resultManifestWritten = false;
                Console.Error.WriteLine($"Result manifest could not be written to '{options.OutputPath}': {ex.Message}");
            }
        }

        WriteErrors(result);

        if (options.Json)
        {
            // --json wins over --quiet: the JSON payload is the point. Print it
            // even when the result manifest file could not be written, so the
            // run's data is not lost.
            Console.Out.WriteLine(ManifestIO.SerializeIngestResultManifest(result));
        }
        else if (!options.Quiet)
        {
            int copiedCount = result.Entries.Count(entry => entry.Error is null);
            int errorCount = result.Errors.Count + result.Entries.Count(entry => entry.Error is not null);
            Console.Out.WriteLine(
                $"Copied {copiedCount} of {result.Entries.Count} files from '{result.SourceFolder}' to '{result.DestinationFolder}'.");
            if (resultManifestWritten && !string.IsNullOrWhiteSpace(options.OutputPath))
            {
                Console.Out.WriteLine($"Result manifest: {Path.GetFullPath(options.OutputPath)}");
            }
            if (errorCount > 0)
            {
                Console.Out.WriteLine($"{errorCount} error(s); see the result manifest for details.");
            }
        }

        return result.Status == ManifestStatus.Success && resultManifestWritten
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
    private static void WriteErrors(IngestResultManifest result)
    {
        foreach (string error in result.Errors)
        {
            Console.Error.WriteLine(error);
        }

        foreach (string? entryError in result.Entries.Select(entry => entry.Error).Where(error => error is not null))
        {
            Console.Error.WriteLine(entryError);
        }
    }
}
