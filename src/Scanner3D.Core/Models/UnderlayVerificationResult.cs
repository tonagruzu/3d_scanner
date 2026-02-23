namespace Scanner3D.Core.Models;

public sealed record UnderlayVerificationResult(
    bool Performed,
    string UnderlayPatternId,
    string DetectionMode,
    double ExpectedBoxSizeMm,
    IReadOnlyList<double> MeasuredBoxSizesMm,
    IReadOnlyList<double> InlierBoxSizesMm,
    double MeanBoxSizeMm,
    double MaxAbsoluteErrorMm,
    double FitConfidence,
    double ScaleConfidence,
    double PoseQuality,
    bool Pass,
    string Notes);
