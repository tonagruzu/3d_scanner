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
    double UnderlayScaleConfidence,
    double UnderlayPoseQuality,
    string Summary);
