namespace Scanner3D.Core.Models;

public sealed record FrameCaptureDiagnostics(
    string BackendUsed,
    bool? ExposureLockVerified,
    bool? WhiteBalanceLockVerified,
    string TimestampSource);

public sealed record FrameCaptureResult(
    IReadOnlyList<CaptureFrame> Frames,
    FrameCaptureDiagnostics Diagnostics);
