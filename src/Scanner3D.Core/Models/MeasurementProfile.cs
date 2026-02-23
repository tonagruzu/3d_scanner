namespace Scanner3D.Core.Models;

public sealed record DimensionReference(
    string Name,
    double ReferenceMm);

public sealed record MeasurementProfile(
    IReadOnlyList<DimensionReference> References,
    string ProfileName,
    string Units = "mm");
