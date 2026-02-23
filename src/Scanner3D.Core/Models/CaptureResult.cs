namespace Scanner3D.Core.Models;

public sealed record CaptureResult(
    string CameraDeviceId,
    CameraCaptureMode SelectedMode,
    int CapturedFrameCount,
    int AcceptedFrameCount,
    IReadOnlyList<CaptureFrame> Frames,
    string Notes);
