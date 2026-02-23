namespace Scanner3D.Core.Models;

public sealed record UnderlayVerificationResult(
    bool Performed,
    string UnderlayPatternId,
    double ExpectedBoxSizeMm,
    IReadOnlyList<double> MeasuredBoxSizesMm,
    double MeanBoxSizeMm,
    double MaxAbsoluteErrorMm,
    bool Pass,
    string Notes);
