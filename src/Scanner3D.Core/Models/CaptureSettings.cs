namespace Scanner3D.Core.Models;

public sealed record CaptureSettings(
    int TargetFrameCount,
    bool LockExposure,
    bool LockWhiteBalance,
    string UnderlayPattern,
    string LightingProfile,
    bool AllowMockFallback = false);
