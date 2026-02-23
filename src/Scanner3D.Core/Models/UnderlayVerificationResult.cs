namespace Scanner3D.Core.Models;

public sealed record UnderlayVerificationResult(
    bool Performed,
    string UnderlayPatternId,
    double ExpectedBoxSizeMm,
    IReadOnlyList<double> MeasuredBoxSizesMm,
    IReadOnlyList<double> InlierBoxSizesMm,
    double MeanBoxSizeMm,
    double MaxAbsoluteErrorMm,
    double FitConfidence,
    bool Pass,
    string Notes);
