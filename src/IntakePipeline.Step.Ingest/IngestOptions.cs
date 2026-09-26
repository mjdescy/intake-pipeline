using CommandLine;

namespace IntakePipeline.Step.Ingest;

/// <summary>
/// Command line options for the ingest step. The single positional argument
/// is the input file manifest; the destination and everything else are flags
/// or options.
/// </summary>
public sealed class IngestOptions
{
    /// <summary>
    /// Path to the input file manifest JSON. Not enforced by the parser so
    /// that --version and --help work without it; <see cref="Program"/>
    /// validates it instead.
    /// </summary>
    [Value(0, Required = false, MetaName = "manifest",
        HelpText = "Path to the input file manifest JSON, e.g. the output of IntakePipeline.Step.Manifest.")]
    public string ManifestPath { get; set; } = "";

    /// <summary>Folder to copy the manifest's files to.</summary>
    [Option('d', "destination", Required = false,
        HelpText = "Folder to copy the manifest's files to, preserving folder structure. Relative paths resolve against the current directory.")]
    public string? DestinationFolder { get; set; }

    /// <summary>Run identifier to record in the result manifest.</summary>
    [Option("run-id", Required = false,
        HelpText = "GUID identifying this run, recorded in the result manifest. Defaults to a new GUID.")]
    public string? RunId { get; set; }

    /// <summary>Path to write the result manifest JSON file to.</summary>
    [Option('o', "output", Required = false,
        HelpText = "Path to write the result manifest JSON to. Required unless --json is used. Parent folders are created as needed.")]
    public string? OutputPath { get; set; }

    /// <summary>Print the result manifest JSON to stdout.</summary>
    [Option("json", Required = false, Default = false,
        HelpText = "Print the result manifest as JSON to stdout. Wins over --quiet.")]
    public bool Json { get; set; }

    /// <summary>Suppress all stdout output.</summary>
    [Option('q', "quiet", Required = false, Default = false,
        HelpText = "Print nothing to stdout; the exit code carries the result. Ignored when --json is set.")]
    public bool Quiet { get; set; }

    /// <summary>Print the application version and exit.</summary>
    [Option("version", Required = false, Default = false,
        HelpText = "Print the application version and exit.")]
    public bool Version { get; set; }
}
