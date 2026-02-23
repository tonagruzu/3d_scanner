using System.Text;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class SketchService : ISketchService
{
    private static readonly string[] Views = ["front", "back", "left", "right", "top", "bottom"];

    public async Task<IReadOnlyList<string>> GenerateOrthographicSketchesAsync(
        Guid sessionId,
        IReadOnlyList<DimensionMeasurement> measurements,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var sketchDirectory = Path.Combine(outputDirectory, "sketches");
        Directory.CreateDirectory(sketchDirectory);

        var generatedPaths = new List<string>(Views.Length);

        foreach (var view in Views)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = Path.Combine(sketchDirectory, $"{view}.svg");
            var svg = BuildSvg(sessionId, view, measurements);
            await File.WriteAllTextAsync(path, svg, Encoding.UTF8, cancellationToken);
            generatedPaths.Add(path);
        }

        return generatedPaths;
    }

    private static string BuildSvg(Guid sessionId, string view, IReadOnlyList<DimensionMeasurement> measurements)
    {
        var lines = new List<string>
        {
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\" viewBox=\"0 0 800 600\">",
            "  <rect x=\"40\" y=\"40\" width=\"720\" height=\"520\" fill=\"none\" stroke=\"black\" stroke-width=\"2\" />",
            $"  <text x=\"60\" y=\"80\" font-size=\"24\" font-family=\"Segoe UI\">View: {view}</text>",
            $"  <text x=\"60\" y=\"115\" font-size=\"14\" font-family=\"Segoe UI\">Session: {sessionId:N}</text>"
        };

        var y = 170;
        foreach (var measurement in measurements)
        {
            lines.Add($"  <text x=\"60\" y=\"{y}\" font-size=\"16\" font-family=\"Segoe UI\">{measurement.Name}: ref {measurement.ReferenceMm:0.###} mm, measured {measurement.MeasuredMm:0.###} mm, error {measurement.AbsoluteErrorMm:0.###} mm</text>");
            y += 30;
        }

        lines.Add("</svg>");
        return string.Join(Environment.NewLine, lines);
    }
}
