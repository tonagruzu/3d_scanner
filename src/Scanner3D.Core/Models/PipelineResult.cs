namespace Scanner3D.Core.Models;

public sealed record PipelineResult(
    bool Success,
    CaptureResult Capture,
    CalibrationResult Calibration,
    UnderlayVerificationResult UnderlayVerification,
    ValidationReport Validation,
    string MeshPath,
    IReadOnlyList<string> SketchPaths,
    string ValidationReportPath,
    string Message);
