namespace Scanner3D.Core.Models;

public sealed record ScanQualityReport(
    Guid SessionId,
    DateTimeOffset GeneratedAt,
    CaptureResult Capture,
    CaptureQualitySummary CaptureQuality,
    UnderlayVerificationResult UnderlayVerification,
    CalibrationResult Calibration,
    CalibrationQualitySummary CalibrationQuality,
    ValidationReport Validation);
