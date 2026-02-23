using Scanner3D.Core.Models;

namespace Scanner3D.Pipeline;

public static class CalibrationGateEvaluator
{
    public static IReadOnlyList<string> Evaluate(
        CalibrationResult calibration,
        CalibrationResidualSamples residualSamples,
        UnderlayVerificationResult underlayVerification,
        bool requireIntrinsicFrames)
    {
        var failures = new List<string>();

        var usedIntrinsicFrames = calibration.IntrinsicCalibration?.UsedFrameIds.Count ?? 0;
        if (requireIntrinsicFrames && usedIntrinsicFrames < CalibrationGateThresholds.MinUsableIntrinsicFrames)
        {
            failures.Add($"intrinsic_frames={usedIntrinsicFrames} < {CalibrationGateThresholds.MinUsableIntrinsicFrames}");
        }

        var reprojectionSamples = residualSamples.ReprojectionResidualSamplesPx;
        if (reprojectionSamples.Count > 0)
        {
            var reprojectionPercentile = CalculatePercentile(reprojectionSamples, CalibrationGateThresholds.ReprojectionErrorPercentile);
            if (reprojectionPercentile > CalibrationGateThresholds.MaxReprojectionErrorPercentilePx)
            {
                failures.Add($"reprojection_p{CalibrationGateThresholds.ReprojectionErrorPercentile}={reprojectionPercentile:0.###} > {CalibrationGateThresholds.MaxReprojectionErrorPercentilePx:0.###}");
            }
        }
        else if (calibration.ReprojectionErrorPx > CalibrationGateThresholds.MaxReprojectionErrorPx)
        {
            failures.Add($"reprojection_error={calibration.ReprojectionErrorPx:0.###} > {CalibrationGateThresholds.MaxReprojectionErrorPx:0.###}");
        }

        if (underlayVerification.ScaleConfidence < CalibrationGateThresholds.MinUnderlayScaleConfidence)
        {
            failures.Add($"scale_confidence={underlayVerification.ScaleConfidence:0.###} < {CalibrationGateThresholds.MinUnderlayScaleConfidence:0.###}");
        }

        if (underlayVerification.PoseQuality < CalibrationGateThresholds.MinUnderlayPoseQuality)
        {
            failures.Add($"pose_quality={underlayVerification.PoseQuality:0.###} < {CalibrationGateThresholds.MinUnderlayPoseQuality:0.###}");
        }

        return failures;
    }

    private static double CalculatePercentile(IReadOnlyList<double> values, int percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(value => value).ToList();
        var rank = (percentile / 100.0) * (sorted.Count - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
        {
            return sorted[lower];
        }

        var weight = rank - lower;
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * weight);
    }
}
