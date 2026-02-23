namespace Scanner3D.Core.Models;

public sealed record CalibrationResidualSamples(
    IReadOnlyList<double> ReprojectionResidualSamplesPx,
    IReadOnlyList<double> ScaleResidualSamplesMm);
