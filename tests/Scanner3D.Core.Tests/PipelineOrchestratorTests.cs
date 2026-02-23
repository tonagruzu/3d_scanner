using Scanner3D.Core.Models;
using Scanner3D.Pipeline;
using System.Text.Json;
using Xunit;

namespace Scanner3D.Core.Tests;

public class PipelineOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSixSketches()
    {
        var orchestrator = new PipelineOrchestrator();
        var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "test-device", "test");

        var result = await orchestrator.ExecuteAsync(session);

        Assert.True(result.Success);
        Assert.Equal(6, result.SketchPaths.Count);
        Assert.False(string.IsNullOrWhiteSpace(result.Capture.CameraDeviceId));
        Assert.True(result.Capture.CapturedFrameCount >= 3);
        Assert.True(result.Calibration.IsWithinTolerance);
        Assert.True(result.UnderlayVerification.Performed);
        Assert.True(result.UnderlayVerification.Pass);
        Assert.Equal(10.0, result.UnderlayVerification.ExpectedBoxSizeMm);
        Assert.True(result.Validation.Pass);
        Assert.Equal(0.5, result.Validation.ToleranceMm);
        Assert.True(result.Capture.AcceptedFrameCount > 0);
        Assert.True(File.Exists(result.ValidationReportPath));

        var outputDirectory = Path.GetDirectoryName(result.ValidationReportPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory) && Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ValidationReport_ContainsMeasurements()
    {
        var orchestrator = new PipelineOrchestrator();
        var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "test-device", "validation-check");

        var result = await orchestrator.ExecuteAsync(session);

        Assert.NotEmpty(result.Validation.Measurements);
        Assert.All(result.Validation.Measurements, measurement =>
            Assert.True(measurement.IsWithinTolerance(result.Validation.ToleranceMm)));
        Assert.True(result.UnderlayVerification.MaxAbsoluteErrorMm <= 0.2);

        await using (var stream = File.OpenRead(result.ValidationReportPath))
        {
            using var document = await JsonDocument.ParseAsync(stream);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("underlayVerification", out _));
            Assert.True(root.TryGetProperty("calibration", out _));
            Assert.True(root.TryGetProperty("validation", out _));
            Assert.True(root.TryGetProperty("capture", out _));
            Assert.True(root.TryGetProperty("captureQuality", out var captureQuality));
            Assert.True(root.TryGetProperty("calibrationQuality", out var calibrationQuality));

            var acceptedRatio = captureQuality.GetProperty("acceptedRatio").GetDouble();
            Assert.True(acceptedRatio > 0);

            var reprojectionSamples = calibrationQuality.GetProperty("reprojectionResidualSamplesPx");
            Assert.True(reprojectionSamples.GetArrayLength() >= 3);
        }

        var outputDirectory = Path.GetDirectoryName(result.ValidationReportPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory) && Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
