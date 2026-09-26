using CommandLine;

namespace IntakePipeline.Step.Manifest;

/// <summary>
/// Command line options for the manifest step. The single positional argument
/// is the folder to manifest; the run id and everything else are flags or
/// options.
/// </summary>
public sealed class ManifestOptions
{
    /// <summary>
    /// Path to the folder to manifest. Not enforced by the parser so that
    /// --version and --help work without it; <see cref="Program"/> validates
    /// it instead.
    /// </summary>
    [Value(0, Required = false, MetaName = "folder",
        HelpText = "Path to the folder to manifest. Every file under it, including subfolders, is described. Relative paths resolve against the current directory.")]
    public string FolderPath { get; set; } = "";

    /// <summary>Run identifier to record in the manifest.</summary>
    [Option("run-id", Required = false,
        HelpText = "GUID identifying this run, recorded in the manifest, e.g. 6f9619ff-8b86-d011-b42d-00cf4fc964ff.")]
    public string? RunId { get; set; }

    /// <summary>Path to write the file manifest JSON file to.</summary>
    [Option('o', "output", Required = false,
        HelpText = "Path to write the file manifest JSON to. Required unless --json is used. Parent folders are created as needed.")]
    public string? OutputPath { get; set; }

    /// <summary>Print the file manifest JSON to stdout.</summary>
    [Option("json", Required = false, Default = false,
        HelpText = "Print the file manifest as JSON to stdout. Wins over --quiet.")]
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
