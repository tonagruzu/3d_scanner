using Scanner3D.Core.Models;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class MeshServiceTests
{
    [Fact]
    public async Task GenerateObjAsync_CreatesObjWithVerticesAndFaces()
    {
        var service = new MeshService();
        var sessionId = Guid.NewGuid();
        var outputDirectory = Path.Combine("output", sessionId.ToString("N"));

        var measurements = new List<DimensionMeasurement>
        {
            new("Width", 44, 43.8, 0.2),
            new("Height", 27, 27.1, 0.1),
            new("Depth", 19, 18.9, 0.1)
        };

        var path = await service.GenerateObjAsync(sessionId, measurements, outputDirectory);

        Assert.True(File.Exists(path));

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("o scanned_object", content);
        Assert.Contains("\nv ", content);
        Assert.Contains("\nf ", content);

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
