namespace Scanner3D.Core.Models;

public sealed record CaptureResult(
    string CameraDeviceId,
    CameraCaptureMode SelectedMode,
    int CapturedFrameCount,
    int AcceptedFrameCount,
    IReadOnlyList<CaptureFrame> Frames,
    string CaptureBackend,
    bool ExposureLockRequested,
    bool WhiteBalanceLockRequested,
    bool? ExposureLockVerified,
    bool? WhiteBalanceLockVerified,
    string FrameTimestampSource,
    bool FrameTimestampsMonotonic,
    string Notes);
