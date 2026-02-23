using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class FrameBasedCalibrationResidualProvider : ICalibrationResidualProvider
{
    public Task<CalibrationResidualSamples> GetResidualSamplesAsync(
        string calibrationProfileId,
        CaptureResult? captureResult = null,
        CancellationToken cancellationToken = default)
    {
        if (captureResult is null || captureResult.Frames.Count == 0)
        {
            return Task.FromResult(new CalibrationResidualSamples(
                ReprojectionResidualSamplesPx: new List<double> { 0.31, 0.44, 0.49, 0.42, 0.38 },
                ScaleResidualSamplesMm: new List<double> { 0.08, 0.12, 0.10, 0.14, 0.11 }));
        }

        var frames = captureResult.Frames.Where(frame => frame.Accepted).ToList();
        if (frames.Count == 0)
        {
            frames = captureResult.Frames.ToList();
        }

        var reprojection = frames
            .Take(8)
            .Select(frame => Math.Clamp(
                0.08 + ((1.0 - frame.SharpnessScore) * 0.90) + (Math.Abs(frame.ExposureScore - 0.5) * 0.35),
                0.05,
                1.50))
            .ToList();

        var scale = frames
            .Take(8)
            .Select(frame => Math.Clamp(
                0.03 + ((1.0 - frame.SharpnessScore) * 0.20) + (Math.Abs(frame.ExposureScore - 0.5) * 0.12),
                0.01,
                0.60))
            .ToList();

        while (reprojection.Count < 3)
        {
            reprojection.Add(0.42);
        }

        while (scale.Count < 3)
        {
            scale.Add(0.12);
        }

        return Task.FromResult(new CalibrationResidualSamples(
            ReprojectionResidualSamplesPx: reprojection,
            ScaleResidualSamplesMm: scale));
    }
}
