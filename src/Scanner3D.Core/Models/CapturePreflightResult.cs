namespace Scanner3D.Core.Models;

public sealed record CapturePreflightResult(
    bool Pass,
    SelectedCameraInfo? SelectedCamera,
    IReadOnlyList<CameraCaptureMode> ModeList,
    string BackendCandidate,
    bool MockFallbackAllowed,
    bool ExposureLockVerificationSupported,
    bool WhiteBalanceLockVerificationSupported,
    string ExposureLockCapabilityStatus,
    string WhiteBalanceLockCapabilityStatus,
    bool TimestampReadinessPass,
    IReadOnlyList<string> BlockingIssues,
    IReadOnlyList<string> Warnings,
    string Summary);
