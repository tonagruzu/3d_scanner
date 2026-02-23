using Scanner3D.Core.Models;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class CalibrationGateEvaluatorTests
{
    [Fact]
    public void Evaluate_Passes_WhenResidualPercentileAndConfidenceMeetThresholds()
    {
        var calibration = BuildCalibration(reprojectionErrorPx: 0.21, usedIntrinsicFrames: 0);
        var residuals = new CalibrationResidualSamples(
            ReprojectionResidualSamplesPx: [0.12, 0.18, 0.22, 0.31, 0.41],
            ScaleResidualSamplesMm: [0.03, 0.04, 0.05]);
        var underlay = BuildUnderlay(scaleConfidence: 0.85, poseQuality: 0.70);

        var failures = CalibrationGateEvaluator.Evaluate(calibration, residuals, underlay, requireIntrinsicFrames: false);

        Assert.Empty(failures);
    }

    [Fact]
    public void Evaluate_Fails_WhenReprojectionPercentileExceedsThreshold()
    {
        var calibration = BuildCalibration(reprojectionErrorPx: 0.32, usedIntrinsicFrames: 0);
        var residuals = new CalibrationResidualSamples(
            ReprojectionResidualSamplesPx: [0.10, 0.12, 0.16, 0.18, 0.85],
            ScaleResidualSamplesMm: [0.03, 0.04, 0.05]);
        var underlay = BuildUnderlay(scaleConfidence: 0.85, poseQuality: 0.72);

        var failures = CalibrationGateEvaluator.Evaluate(calibration, residuals, underlay, requireIntrinsicFrames: false);

        Assert.Contains(failures, failure => failure.StartsWith("reprojection_p95=", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_Fails_WhenStrictIntrinsicGateEnabledAndFramesAreMissing()
    {
        var calibration = BuildCalibration(reprojectionErrorPx: 0.25, usedIntrinsicFrames: 1);
        var residuals = new CalibrationResidualSamples(
            ReprojectionResidualSamplesPx: [0.14, 0.20, 0.24, 0.27],
            ScaleResidualSamplesMm: [0.03, 0.04, 0.05]);
        var underlay = BuildUnderlay(scaleConfidence: 0.86, poseQuality: 0.74);

        var failures = CalibrationGateEvaluator.Evaluate(calibration, residuals, underlay, requireIntrinsicFrames: true);

        Assert.Contains(failures, failure => failure.Contains("intrinsic_frames=1", StringComparison.Ordinal));
    }

    private static CalibrationResult BuildCalibration(double reprojectionErrorPx, int usedIntrinsicFrames)
    {
        var used = Enumerable.Range(1, usedIntrinsicFrames).Select(index => $"f-{index:000}").ToList();
        var intrinsic = new IntrinsicCalibrationDetails(
            PatternType: "checkerboard",
            PatternColumns: 9,
            PatternRows: 6,
            SquareSizeMm: 10.0,
            ImageWidthPx: 1280,
            ImageHeightPx: 720,
            CameraMatrix: [800, 0, 640, 0, 800, 360, 0, 0, 1],
            DistortionCoefficients: [0, 0, 0, 0, 0],
            UsedFrameIds: used,
            RejectedFrameReasons: []);

        return new CalibrationResult(
            CalibrationProfileId: "calib-test",
            CalibratedAt: DateTimeOffset.UtcNow,
            ReprojectionErrorPx: reprojectionErrorPx,
            ScaleErrorMm: 0.08,
            IsWithinTolerance: true,
            Notes: "test",
            IntrinsicCalibration: intrinsic);
    }

    private static UnderlayVerificationResult BuildUnderlay(double scaleConfidence, double poseQuality)
        => new(
            Performed: true,
            UnderlayPatternId: "Mata-10mm-grid",
            DetectionMode: "preview-image",
            ExpectedBoxSizeMm: 10.0,
            MeasuredBoxSizesMm: [9.98, 10.01, 10.02],
            InlierBoxSizesMm: [9.98, 10.01, 10.02],
            MeanBoxSizeMm: 10.003,
            MaxAbsoluteErrorMm: 0.02,
            FitConfidence: 0.92,
            ScaleConfidence: scaleConfidence,
            PoseQuality: poseQuality,
            Pass: true,
            Notes: "test");
}
