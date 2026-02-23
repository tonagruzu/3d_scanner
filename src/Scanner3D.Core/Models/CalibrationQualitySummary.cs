namespace Scanner3D.Core.Models;

public sealed record CalibrationQualitySummary(
    double ReprojectionErrorPx,
    double ScaleErrorMm,
    IReadOnlyList<double> ReprojectionResidualSamplesPx,
    IReadOnlyList<double> ScaleResidualSamplesMm,
    bool GatePass,
    IReadOnlyList<string> GateFailures,
    int UsedIntrinsicFrames,
    int MinimumRequiredIntrinsicFrames,
    int IntrinsicFramesEvaluated,
    int IntrinsicFramesRejected,
    IReadOnlyDictionary<string, int> IntrinsicRejectedFramesByReason,
    IReadOnlyDictionary<string, int> IntrinsicRejectedFramesByCategory,
    IReadOnlyList<IntrinsicFrameInclusionDiagnostic> IntrinsicFrameDiagnostics,
    double UnderlayScaleConfidence,
    double UnderlayPoseQuality,
    string Summary);
