using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class PipelineOrchestrator : IPipelineOrchestrator
{
    public async Task<PipelineResult> ExecuteAsync(ScanSession session, CancellationToken cancellationToken = default)
    {
        var underlayValidator = new UnderlayPatternValidator();
        var validationWriter = new JsonValidationReportWriter();
        var captureQualityAnalyzer = new CaptureQualityAnalyzer();

        var capturedFrames = new List<CaptureFrame>
        {
            new("f-001", DateTimeOffset.UtcNow, SharpnessScore: 0.93, ExposureScore: 0.91, Accepted: true),
            new("f-002", DateTimeOffset.UtcNow, SharpnessScore: 0.89, ExposureScore: 0.90, Accepted: true),
            new("f-003", DateTimeOffset.UtcNow, SharpnessScore: 0.72, ExposureScore: 0.88, Accepted: false)
        };

        var capture = new CaptureResult(
            CameraDeviceId: session.CameraDeviceId,
            CapturedFrameCount: capturedFrames.Count,
            AcceptedFrameCount: capturedFrames.Count(frame => frame.Accepted),
            Frames: capturedFrames,
            Notes: "Stub capture summary. Replace with camera integration.");

        var calibration = new CalibrationResult(
            CalibrationProfileId: "calib-profile-bootstrap",
            CalibratedAt: DateTimeOffset.UtcNow,
            ReprojectionErrorPx: 0.42,
            ScaleErrorMm: 0.12,
            IsWithinTolerance: true,
            Notes: "Stub calibration summary. Replace with real solve and scale checks.");

        var calibrationQuality = new CalibrationQualitySummary(
            ReprojectionErrorPx: calibration.ReprojectionErrorPx,
            ScaleErrorMm: calibration.ScaleErrorMm,
            ReprojectionResidualSamplesPx: new List<double> { 0.31, 0.44, 0.49, 0.42, 0.38 },
            ScaleResidualSamplesMm: new List<double> { 0.08, 0.12, 0.10, 0.14, 0.11 },
            Summary: calibration.IsWithinTolerance
                ? "Calibration quality is within configured tolerance limits."
                : "Calibration quality is outside tolerance limits.");

        var captureQuality = captureQualityAnalyzer.Analyze(capture);

        var underlayVerification = underlayValidator.Validate(
            underlayPatternId: "Mata-10mm-grid",
            expectedBoxSizeMm: 10.0,
            measuredBoxSizesMm: new List<double> { 9.96, 10.04, 10.07, 9.98, 10.02 },
            toleranceMm: 0.2);

        const double toleranceMm = 0.5;
        var measurements = new List<DimensionMeasurement>
        {
            new("Width", ReferenceMm: 44.00, MeasuredMm: 43.76, AbsoluteErrorMm: 0.24),
            new("Height", ReferenceMm: 27.00, MeasuredMm: 27.31, AbsoluteErrorMm: 0.31),
            new("Depth", ReferenceMm: 19.00, MeasuredMm: 18.87, AbsoluteErrorMm: 0.13)
        };

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

        var sketches = new List<string>
        {
            "front.svg",
            "back.svg",
            "left.svg",
            "right.svg",
            "top.svg",
            "bottom.svg"
        };

        var success = calibration.IsWithinTolerance && underlayVerification.Pass && validation.Pass;
        var message = success
            ? "Pipeline stub executed. Underlay, calibration, and dimensional checks are within configured tolerances."
            : "Pipeline stub executed with failed quality gates. Review underlay, calibration, and validation outputs.";

        var outputDirectory = Path.Combine("output", session.SessionId.ToString("N"));
        var qualityReport = new ScanQualityReport(
            SessionId: session.SessionId,
            GeneratedAt: DateTimeOffset.UtcNow,
            Capture: capture,
            CaptureQuality: captureQuality,
            UnderlayVerification: underlayVerification,
            Calibration: calibration,
            CalibrationQuality: calibrationQuality,
            Validation: validation);

        var validationReportPath = await validationWriter.WriteAsync(qualityReport, outputDirectory, cancellationToken);

        var result = new PipelineResult(
            Success: success,
            Capture: capture,
            Calibration: calibration,
            UnderlayVerification: underlayVerification,
            Validation: validation,
            MeshPath: "output/model.obj",
            SketchPaths: sketches,
            ValidationReportPath: validationReportPath,
            Message: message);

        return result;
    }
}
