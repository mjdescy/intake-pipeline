namespace IntakePipeline.Step.Ingest;

/// <summary>
/// Exit codes map to distinct failure modes so scripts can react precisely:
/// 0 success, 1 the user made a mistake, 2 the run itself failed.
/// </summary>
internal enum ExitCode
{
    Success = 0,
    UsageError = 1,
    RuntimeError = 2,
}
