namespace IntakePipeline.Core.Manifest;

/// <summary>
/// Status values for a manifest run. Kept as constants so that the manifest
/// JSON and the code never drift apart.
/// </summary>
public static class ManifestStatus
{
    /// <summary>Every discovered file was described cleanly, with no global errors.</summary>
    public const string Success = "success";

    /// <summary>At least one global error or per-file error occurred.</summary>
    public const string Failure = "failure";
}
