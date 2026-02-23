namespace Scanner3D.Core.Models;

public sealed record CalibrationQualitySummary(
    double ReprojectionErrorPx,
    double ScaleErrorMm,
    IReadOnlyList<double> ReprojectionResidualSamplesPx,
    IReadOnlyList<double> ScaleResidualSamplesMm,
    string Summary);
