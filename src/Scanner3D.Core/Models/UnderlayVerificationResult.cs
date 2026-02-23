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
    string Notes,
    double GridSpacingPx = 0,
    double GridSpacingStdDevPx = 0,
    double HomographyInlierRatio = 0,
    double PoseReprojectionErrorPx = 0,
    bool GeometryDerived = false);
