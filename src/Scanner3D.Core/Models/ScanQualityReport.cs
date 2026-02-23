namespace Scanner3D.Core.Models;

public sealed record SelectedCameraInfo(
    string DeviceId,
    string DisplayName);

public sealed record CaptureCapabilityDetails(
    SelectedCameraInfo SelectedCamera,
    IReadOnlyList<CameraCaptureMode> ModeList,
    string BackendUsed);

public sealed record ScanQualityReport(
    Guid SessionId,
    DateTimeOffset GeneratedAt,
    CaptureResult Capture,
    CaptureQualitySummary CaptureQuality,
    UnderlayVerificationResult UnderlayVerification,
    CalibrationResult Calibration,
    CalibrationQualitySummary CalibrationQuality,
    ValidationReport Validation,
    CaptureCapabilityDetails? CaptureCapabilities = null);
