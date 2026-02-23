namespace Scanner3D.Core.Models;

public sealed record DimensionMeasurement(
    string Name,
    double ReferenceMm,
    double MeasuredMm,
    double AbsoluteErrorMm)
{
    public bool IsWithinTolerance(double toleranceMm) => AbsoluteErrorMm <= toleranceMm;
}
