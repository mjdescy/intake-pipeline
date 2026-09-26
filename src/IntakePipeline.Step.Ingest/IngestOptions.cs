using CommandLine;

namespace IntakePipeline.Step.Ingest;

/// <summary>
/// Command line options for the ingest step. The single positional argument
/// is the input manifest; everything else is a flag or option.
/// </summary>
public sealed class IngestOptions
{
    /// <summary>
    /// Path to the input manifest JSON file. Not enforced by the parser so
    /// that --version and --help work without it; <see cref="Program"/>
    /// validates it instead.
    /// </summary>
    [Value(0, Required = false, MetaName = "manifest",
        HelpText = "Path to the input manifest JSON file, e.g. ingest-manifest.json. Relative 'sourceFolder' and 'destinationFolder' paths inside it resolve against the manifest's own folder.")]
    public string ManifestPath { get; set; } = "";

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
