namespace Scanner3D.Core.Models;

public sealed record ValidationReport(
    Guid SessionId,
    DateTimeOffset GeneratedAt,
    double ToleranceMm,
    IReadOnlyList<DimensionMeasurement> Measurements,
    double MaxAbsoluteErrorMm,
    double MeanAbsoluteErrorMm,
    bool Pass,
    string Summary);
