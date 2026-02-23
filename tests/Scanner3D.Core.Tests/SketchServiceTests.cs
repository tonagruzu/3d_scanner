using Scanner3D.Core.Models;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class SketchServiceTests
{
    [Fact]
    public async Task GenerateOrthographicSketchesAsync_CreatesSixSvgFiles()
    {
        var service = new SketchService();
        var sessionId = Guid.NewGuid();
        var outputDirectory = Path.Combine("output", sessionId.ToString("N"));

        var measurements = new List<DimensionMeasurement>
        {
            new("Width", 44, 43.8, 0.2),
            new("Height", 27, 27.1, 0.1),
            new("Depth", 19, 18.9, 0.1)
        };

        var sketches = await service.GenerateOrthographicSketchesAsync(sessionId, measurements, outputDirectory);

        Assert.Equal(6, sketches.Count);
        Assert.All(sketches, sketchPath => Assert.True(File.Exists(sketchPath)));
        Assert.Contains(sketches, path => path.EndsWith("front.svg", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sketches, path => path.EndsWith("back.svg", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sketches, path => path.EndsWith("left.svg", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sketches, path => path.EndsWith("right.svg", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sketches, path => path.EndsWith("top.svg", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sketches, path => path.EndsWith("bottom.svg", StringComparison.OrdinalIgnoreCase));

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
