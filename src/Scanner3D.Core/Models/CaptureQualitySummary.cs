namespace Scanner3D.Core.Models;

public sealed record CaptureQualitySummary(
    int TotalFrames,
    int AcceptedFrames,
    double AcceptedRatio,
    double MeanSharpness,
    double MeanExposure,
    IReadOnlyDictionary<string, int> RejectionCounts,
    string Summary);
