using IntakePipeline.Core.Manifest;
using IntakePipeline.Step.Manifest;

namespace IntakePipeline.Step.Ingest.Tests;

/// <summary>
/// End-to-end acceptance test for the shared manifest. Ingest's output must be
/// exactly what the Manifest step would produce for the destination, so the
/// two steps can compose without a translation layer.
/// </summary>
public sealed class ManifestPipelineIntegrationTests
{
    [Fact]
    public void IngestedManifest_MatchesManifestStepOutputForDestination()
    {
        using TempDirectory directory = new();
        string sourceFolder = Path.Combine(directory.FullPath, "source");
        Directory.CreateDirectory(Path.Combine(sourceFolder, "sub", "deep"));
        File.WriteAllBytes(Path.Combine(sourceFolder, "a.txt"), "hello intake"u8.ToArray());
        File.WriteAllBytes(Path.Combine(sourceFolder, "sub", "b.bin"), [0x00, 0x01, 0x02, 0xff]);
        File.WriteAllBytes(Path.Combine(sourceFolder, "sub", "deep", "c.txt"), "deep"u8.ToArray());
        File.WriteAllBytes(Path.Combine(sourceFolder, "empty.bin"), []);
        string destinationFolder = Path.Combine(directory.FullPath, "dest");
        string inputManifestPath = Path.Combine(directory.FullPath, "source-manifest.json");

        FileManifest ingestedSource = ManifestRunner.Run(Guid.NewGuid(), sourceFolder);
        ManifestIO.WriteFileManifest(ingestedSource, inputManifestPath);

        FileManifest ingested = IngestRunner.Run(
            ingestedSource, destinationFolder, Guid.NewGuid(), inputManifestPath);
        FileManifest manifested = ManifestRunner.Run(Guid.NewGuid(), destinationFolder);

        Assert.Equal(ingested.Folder, manifested.Folder);
        Assert.Equal(ingested.Status, manifested.Status);
        Assert.Equal(ingested.Entries.Count, manifested.Entries.Count);
        Assert.All(
            ingested.Entries.Zip(manifested.Entries),
            pair => AssertEntriesMatch(pair.First, pair.Second));
    }

    private static void AssertEntriesMatch(ManifestFileEntry ingested, ManifestFileEntry manifested)
    {
        Assert.Equal(ingested.FilePath, manifested.FilePath);
        Assert.Equal(ingested.FileName, manifested.FileName);
        Assert.Equal(ingested.Sha256, manifested.Sha256);
        Assert.Equal(ingested.FileSizeInBytes, manifested.FileSizeInBytes);
        Assert.Equal(ingested.FileExtension, manifested.FileExtension);
        Assert.Equal(ingested.Error, manifested.Error);
    }
}
