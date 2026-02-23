namespace Scanner3D.Core.Models;

public sealed record ScanQualityReport(
    Guid SessionId,
    DateTimeOffset GeneratedAt,
    UnderlayVerificationResult UnderlayVerification,
    CalibrationResult Calibration,
    ValidationReport Validation);
