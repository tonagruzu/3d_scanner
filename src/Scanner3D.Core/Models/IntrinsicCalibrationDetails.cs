namespace Scanner3D.Core.Models;

public sealed record IntrinsicCalibrationDetails(
    string PatternType,
    int PatternColumns,
    int PatternRows,
    double SquareSizeMm,
    int ImageWidthPx,
    int ImageHeightPx,
    IReadOnlyList<double> CameraMatrix,
    IReadOnlyList<double> DistortionCoefficients,
    IReadOnlyList<string> UsedFrameIds,
    IReadOnlyList<string> RejectedFrameReasons,
    IReadOnlyDictionary<string, int> RejectedFrameReasonCounts,
    IReadOnlyDictionary<string, int> RejectedFrameCategoryCounts,
    IReadOnlyList<IntrinsicFrameInclusionDiagnostic> FrameDiagnostics);
