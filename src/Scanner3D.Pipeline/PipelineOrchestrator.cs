using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class PipelineOrchestrator : IPipelineOrchestrator
{
    public async Task<PipelineResult> ExecuteAsync(ScanSession session, CancellationToken cancellationToken = default)
    {
        var captureService = new CaptureService();
        var capturePreflightService = new CapturePreflightService();
        var calibrationService = new CalibrationService();
        var calibrationResidualProvider = new FrameBasedCalibrationResidualProvider();
        var measurementService = new MeasurementService();
        var meshService = new MeshService();
        var sketchService = new SketchService();
        var underlayValidator = new UnderlayPatternValidator();
        var underlayBoxEstimator = new UnderlayBoxSizeEstimator();
        var validationWriter = new JsonValidationReportWriter();
        var captureQualityAnalyzer = new CaptureQualityAnalyzer();

        var captureSettings = new CaptureSettings(
            TargetFrameCount: 12,
            LockExposure: true,
            LockWhiteBalance: true,
            UnderlayPattern: "Mata-10mm-grid",
            LightingProfile: "diffuse-white-5600k",
            AllowMockFallback: IsMockFallbackAllowed(session));

        var capturePreflight = await capturePreflightService.EvaluateAsync(session, captureSettings, cancellationToken);
        if (!capturePreflight.Pass)
        {
            var reasons = string.Join(" | ", capturePreflight.BlockingIssues);
            throw new InvalidOperationException($"Capture preflight failed: {reasons}");
        }

        var captureSession = capturePreflight.SelectedCamera is null
            ? session
            : session with { CameraDeviceId = capturePreflight.SelectedCamera.DeviceId };
        var routedCaptureSettings = captureSettings with { PreferredBackend = capturePreflight.BackendCandidate };

        var capture = await captureService.CaptureAsync(captureSession, routedCaptureSettings, cancellationToken);

        var calibration = await calibrationService.CalibrateAsync(session, capture, cancellationToken);
        var residualSamples = await calibrationResidualProvider.GetResidualSamplesAsync(calibration.CalibrationProfileId, capture, cancellationToken);

        var captureQuality = captureQualityAnalyzer.Analyze(capture);

        var expectedUnderlayBoxSizeMm = 10.0;
        var underlayEstimate = underlayBoxEstimator.EstimateMeasuredBoxSizesMm(
            capture,
            expectedUnderlayBoxSizeMm,
            calibration.IntrinsicCalibration);
        var underlayVerification = underlayValidator.Validate(
            underlayPatternId: "Mata-10mm-grid",
            detectionMode: underlayEstimate.DetectionMode,
            expectedBoxSizeMm: expectedUnderlayBoxSizeMm,
            measuredBoxSizesMm: underlayEstimate.MeasuredBoxSizesMm,
            scaleConfidence: underlayEstimate.ScaleConfidence,
            poseQuality: underlayEstimate.PoseQuality,
            toleranceMm: 0.2);

        var usedIntrinsicFrames = calibration.IntrinsicCalibration?.UsedFrameIds.Count ?? 0;
        var intrinsicDiagnostics = calibration.IntrinsicDiagnostics;
        var requireIntrinsicFrames = IsStrictIntrinsicGateRequired(session);
        var calibrationGateFailures = CalibrationGateEvaluator.Evaluate(
            calibration,
            residualSamples,
            underlayVerification,
            requireIntrinsicFrames);

        var calibrationGatePass = calibrationGateFailures.Count == 0;
        var calibrationQuality = new CalibrationQualitySummary(
            ReprojectionErrorPx: calibration.ReprojectionErrorPx,
            ScaleErrorMm: calibration.ScaleErrorMm,
            ReprojectionResidualSamplesPx: residualSamples.ReprojectionResidualSamplesPx,
            ScaleResidualSamplesMm: residualSamples.ScaleResidualSamplesMm,
            GatePass: calibrationGatePass,
            GateFailures: calibrationGateFailures,
            UsedIntrinsicFrames: usedIntrinsicFrames,
            MinimumRequiredIntrinsicFrames: CalibrationGateThresholds.MinUsableIntrinsicFrames,
            IntrinsicFramesEvaluated: intrinsicDiagnostics?.TotalFramesEvaluated ?? 0,
            IntrinsicFramesRejected: intrinsicDiagnostics?.RejectedFrames ?? 0,
            IntrinsicRejectedFramesByReason: intrinsicDiagnostics?.RejectedFramesByReason ?? new Dictionary<string, int>(),
            IntrinsicRejectedFramesByCategory: intrinsicDiagnostics?.RejectedFramesByCategory ?? new Dictionary<string, int>(),
            IntrinsicFrameDiagnostics: intrinsicDiagnostics?.FrameDiagnostics ?? [],
            UnderlayScaleConfidence: underlayVerification.ScaleConfidence,
            UnderlayPoseQuality: underlayVerification.PoseQuality,
            Summary: calibrationGatePass
                ? "Calibration quality gates passed."
                : $"Calibration quality gate failed: {string.Join("; ", calibrationGateFailures)}");

        const double toleranceMm = 0.5;
        var measurementProfile = new MeasurementProfile(
            References:
            [
                new DimensionReference("Width", 44.00),
                new DimensionReference("Height", 27.00),
                new DimensionReference("Depth", 19.00)
            ],
            ProfileName: "baseline-prismatic-part");

        var measurements = await measurementService.MeasureAsync(measurementProfile, calibration, cancellationToken);

        var maxError = measurements.Max(m => m.AbsoluteErrorMm);
        var meanError = measurements.Average(m => m.AbsoluteErrorMm);
        var pass = maxError <= toleranceMm;

        var validation = new ValidationReport(
            SessionId: session.SessionId,
            GeneratedAt: DateTimeOffset.UtcNow,
            ToleranceMm: toleranceMm,
            Measurements: measurements,
            MaxAbsoluteErrorMm: maxError,
            MeanAbsoluteErrorMm: meanError,
            Pass: pass,
            Summary: pass
                ? "Validation pass: all measured dimensions are within ±0.5 mm."
                : "Validation fail: one or more measured dimensions exceed ±0.5 mm.");

        var outputDirectory = Path.Combine("output", session.SessionId.ToString("N"));
            var meshPath = await meshService.GenerateObjAsync(session.SessionId, measurements, outputDirectory, cancellationToken);
        var sketches = await sketchService.GenerateOrthographicSketchesAsync(session.SessionId, measurements, outputDirectory, cancellationToken);

        var captureGatePass = capture.ReliabilityTargetMet;
        var success = captureGatePass && calibrationGatePass && underlayVerification.Pass && validation.Pass;
        var message = success
            ? "Pipeline stub executed. Capture, underlay, calibration, and dimensional checks are within configured tolerances."
            : $"Pipeline stub executed with failed quality gates. Capture gate detail: {capture.ReliabilityFailureReason ?? "n/a"}; Calibration gate detail: {(calibrationGatePass ? "passed" : string.Join(" | ", calibrationGateFailures))}; Underlay pass: {underlayVerification.Pass}; Validation pass: {validation.Pass}";

        var qualityReport = new ScanQualityReport(
            SessionId: session.SessionId,
            GeneratedAt: DateTimeOffset.UtcNow,
            CapturePreflight: capturePreflight,
            Capture: capture,
            CaptureQuality: captureQuality,
            UnderlayVerification: underlayVerification,
            Calibration: calibration,
            CalibrationQuality: calibrationQuality,
            Validation: validation);

        var validationReportPath = await validationWriter.WriteAsync(qualityReport, outputDirectory, cancellationToken);

        var result = new PipelineResult(
            Success: success,
            CapturePreflight: capturePreflight,
            Capture: capture,
            Calibration: calibration,
            CalibrationQuality: calibrationQuality,
            UnderlayVerification: underlayVerification,
            Validation: validation,
            MeshPath: meshPath,
            SketchPaths: sketches,
            ValidationReportPath: validationReportPath,
            Message: message);

        return result;
    }

    private static bool IsMockFallbackAllowed(ScanSession session)
    {
        var environmentOverride = Environment.GetEnvironmentVariable("SCANNER3D_ALLOW_MOCK_FALLBACK");
        if (string.Equals(environmentOverride, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentOverride, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentOverride, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return session.OperatorNotes.Contains("test", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStrictIntrinsicGateRequired(ScanSession session)
    {
        var environmentOverride = Environment.GetEnvironmentVariable("SCANNER3D_REQUIRE_INTRINSIC_FRAMES");
        if (string.Equals(environmentOverride, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentOverride, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentOverride, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return session.OperatorNotes.Contains("require-intrinsic", StringComparison.OrdinalIgnoreCase)
               || session.OperatorNotes.Contains("calibration-strict", StringComparison.OrdinalIgnoreCase);
    }
}
