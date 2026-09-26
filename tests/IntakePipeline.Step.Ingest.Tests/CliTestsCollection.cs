namespace IntakePipeline.Step.Ingest.Tests;

/// <summary>
/// Named collection that serializes the CLI tests: they redirect the
/// process-wide Console streams, so they must never run in parallel with
/// each other.
/// </summary>
[CollectionDefinition("cli")]
public sealed class CliTestsCollection;
