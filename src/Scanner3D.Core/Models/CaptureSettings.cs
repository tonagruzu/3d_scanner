namespace Scanner3D.Core.Models;

public sealed record CaptureSettings(
    int TargetFrameCount,
    bool LockExposure,
    bool LockWhiteBalance,
    string UnderlayPattern,
    string LightingProfile,
    bool AllowMockFallback = false,
    string? PreferredBackend = null,
    int MinimumAcceptedFrameCount = 8,
    int MaxCaptureAttempts = 3);
