namespace Scanner3D.Core.Models;

public sealed record CaptureResult(
    string CameraDeviceId,
    CameraCaptureMode SelectedMode,
    int CapturedFrameCount,
    int AcceptedFrameCount,
    int RequiredAcceptedFrameCount,
    int CaptureAttemptsUsed,
    int MaxCaptureAttempts,
    bool ReliabilityTargetMet,
    string? ReliabilityFailureReason,
    IReadOnlyList<CaptureFrame> Frames,
    string CaptureBackend,
    bool ExposureLockRequested,
    bool WhiteBalanceLockRequested,
    bool? ExposureLockVerified,
    bool? WhiteBalanceLockVerified,
    string FrameTimestampSource,
    bool FrameTimestampsMonotonic,
    string Notes);
