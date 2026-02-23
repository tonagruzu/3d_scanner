namespace Scanner3D.Core.Models;

public sealed record PipelineResult(
    bool Success,
    CapturePreflightResult? CapturePreflight,
    CaptureResult Capture,
    CalibrationResult Calibration,
    CalibrationQualitySummary CalibrationQuality,
    UnderlayVerificationResult UnderlayVerification,
    ValidationReport Validation,
    string MeshPath,
    IReadOnlyList<string> SketchPaths,
    string ValidationReportPath,
    string Message);
