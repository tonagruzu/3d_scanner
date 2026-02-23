using System.Globalization;
using System.Text;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class MeshService : IMeshService
{
    public async Task<string> GenerateObjAsync(
        Guid sessionId,
        IReadOnlyList<DimensionMeasurement> measurements,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var objPath = Path.Combine(outputDirectory, "model.obj");

        var width = ResolveDimension(measurements, "Width", 44.0);
        var height = ResolveDimension(measurements, "Height", 27.0);
        var depth = ResolveDimension(measurements, "Depth", 19.0);

        var halfWidth = width / 2.0;
        var halfHeight = height / 2.0;
        var halfDepth = depth / 2.0;

        var lines = new List<string>
        {
            "# Scanner3D generated mesh placeholder",
            $"# session {sessionId:N}",
            "o scanned_object",
            Vertex(-halfWidth, -halfHeight, -halfDepth),
            Vertex( halfWidth, -halfHeight, -halfDepth),
            Vertex( halfWidth,  halfHeight, -halfDepth),
            Vertex(-halfWidth,  halfHeight, -halfDepth),
            Vertex(-halfWidth, -halfHeight,  halfDepth),
            Vertex( halfWidth, -halfHeight,  halfDepth),
            Vertex( halfWidth,  halfHeight,  halfDepth),
            Vertex(-halfWidth,  halfHeight,  halfDepth),
            "f 1 2 3",
            "f 1 3 4",
            "f 5 6 7",
            "f 5 7 8",
            "f 1 2 6",
            "f 1 6 5",
            "f 2 3 7",
            "f 2 7 6",
            "f 3 4 8",
            "f 3 8 7",
            "f 4 1 5",
            "f 4 5 8"
        };

        await File.WriteAllLinesAsync(objPath, lines, Encoding.UTF8, cancellationToken);
        return objPath;
    }

    private static double ResolveDimension(IReadOnlyList<DimensionMeasurement> measurements, string name, double fallback)
    {
        return measurements.FirstOrDefault(measurement => string.Equals(measurement.Name, name, StringComparison.OrdinalIgnoreCase))?.MeasuredMm ?? fallback;
    }

    private static string Vertex(double x, double y, double z)
    {
        return string.Format(CultureInfo.InvariantCulture, "v {0:0.###} {1:0.###} {2:0.###}", x, y, z);
    }
}
