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
        var timestampedFrames = captureResult.Frames
            .Where(frame => frame.SourceTimestampMs.HasValue)
            .OrderBy(frame => frame.SourceTimestampMs!.Value)
            .ToList();
        var timestampCoverageRatio = total == 0 ? 0 : (double)timestampedFrames.Count / total;

        var intervals = new List<double>();
        for (var index = 1; index < timestampedFrames.Count; index++)
        {
            var delta = timestampedFrames[index].SourceTimestampMs!.Value
                        - timestampedFrames[index - 1].SourceTimestampMs!.Value;
            if (delta > 0)
            {
                intervals.Add(delta);
            }
        }

        var meanInterFrameIntervalMs = intervals.Count == 0 ? 0 : intervals.Average();
        var interFrameIntervalJitterMs = intervals.Count == 0
            ? 0
            : Math.Sqrt(intervals.Average(value => Math.Pow(value - meanInterFrameIntervalMs, 2)));

        var rejectedCount = total - accepted;
        var rejectionCounts = new Dictionary<string, int>
        {
            ["manual_reject"] = rejectedCount
        };

        var reliabilityWarnings = new List<string>();
        if (total < 3)
        {
            reliabilityWarnings.Add("Too few frames captured for reliable session quality.");
        }

        if (acceptedRatio < 0.5)
        {
            reliabilityWarnings.Add("Accepted frame ratio is below 0.5.");
        }

        if (captureResult.ExposureLockRequested && captureResult.ExposureLockVerified != true)
        {
            reliabilityWarnings.Add("Exposure lock was requested but could not be verified.");
        }

        if (captureResult.WhiteBalanceLockRequested && captureResult.WhiteBalanceLockVerified != true)
        {
            reliabilityWarnings.Add("White balance lock was requested but could not be verified.");
        }

        if (!captureResult.FrameTimestampsMonotonic)
        {
            reliabilityWarnings.Add("Frame timestamps are not monotonic.");
        }

        if (captureResult.CapturedFrameCount > 0 && timestampCoverageRatio < 1.0)
        {
            reliabilityWarnings.Add("Backend-native timestamps are missing for one or more frames.");
        }

        if (intervals.Count > 1 && interFrameIntervalJitterMs > 10)
        {
            reliabilityWarnings.Add("Inter-frame timing jitter exceeds 10 ms.");
        }

        if (!captureResult.ReliabilityTargetMet)
        {
            reliabilityWarnings.Add(captureResult.ReliabilityFailureReason
                                    ?? "Capture reliability target was not met.");
        }

        var reliabilityPass = reliabilityWarnings.Count == 0;

        var summary = acceptedRatio >= 0.8
            ? (reliabilityPass
                ? "Capture quality is acceptable for reconstruction."
                : "Capture quality appears acceptable, but reliability checks reported warnings.")
            : "Capture quality may be insufficient; consider retakes.";

        return new CaptureQualitySummary(
            TotalFrames: total,
            AcceptedFrames: accepted,
            AcceptedRatio: acceptedRatio,
            MeanSharpness: meanSharpness,
            MeanExposure: meanExposure,
            TimestampCoverageRatio: timestampCoverageRatio,
            MeanInterFrameIntervalMs: meanInterFrameIntervalMs,
            InterFrameIntervalJitterMs: interFrameIntervalJitterMs,
            RejectionCounts: rejectionCounts,
            ReliabilityPass: reliabilityPass,
            ReliabilityWarnings: reliabilityWarnings,
            Summary: summary);
    }
}
