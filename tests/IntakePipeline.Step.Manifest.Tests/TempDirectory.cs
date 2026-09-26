namespace IntakePipeline.Step.Manifest.Tests;

/// <summary>
/// Creates a unique temporary directory for a test and deletes it
/// recursively on dispose. Cleanup failures are ignored: a failing cleanup
/// must never fail a test.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public string FullPath { get; } =
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(FullPath);

    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort only.
        }
    }
}
