namespace Scanner3D.Core.Models;

public sealed record CaptureQualitySummary(
    int TotalFrames,
    int AcceptedFrames,
    double AcceptedRatio,
    double MeanSharpness,
    double MeanExposure,
    double TimestampCoverageRatio,
    double MeanInterFrameIntervalMs,
    double InterFrameIntervalJitterMs,
    IReadOnlyDictionary<string, int> RejectionCounts,
    bool ReliabilityPass,
    IReadOnlyList<string> ReliabilityWarnings,
    string Summary);
