using Scanner3D.Core.Models;

namespace Scanner3D.Pipeline;

public sealed class CaptureQualityAnalyzer
{
    public CaptureQualitySummary Analyze(CaptureResult captureResult)
    {
        var total = captureResult.Frames.Count;
        var accepted = captureResult.Frames.Count(frame => frame.Accepted);
        var acceptedRatio = total == 0 ? 0 : (double)accepted / total;

        var meanSharpness = total == 0 ? 0 : captureResult.Frames.Average(frame => frame.SharpnessScore);
        var meanExposure = total == 0 ? 0 : captureResult.Frames.Average(frame => frame.ExposureScore);

        var rejectedCount = total - accepted;
        var rejectionCounts = new Dictionary<string, int>
        {
            ["manual_reject"] = rejectedCount
        };

        var summary = acceptedRatio >= 0.8
            ? "Capture quality is acceptable for reconstruction."
            : "Capture quality may be insufficient; consider retakes.";

        return new CaptureQualitySummary(
            TotalFrames: total,
            AcceptedFrames: accepted,
            AcceptedRatio: acceptedRatio,
            MeanSharpness: meanSharpness,
            MeanExposure: meanExposure,
            RejectionCounts: rejectionCounts,
            Summary: summary);
    }
}
