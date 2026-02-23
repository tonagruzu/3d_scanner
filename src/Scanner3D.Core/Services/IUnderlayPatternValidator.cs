using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface IUnderlayPatternValidator
{
    UnderlayVerificationResult Validate(
        string underlayPatternId,
        string detectionMode,
        double expectedBoxSizeMm,
        IReadOnlyList<double> measuredBoxSizesMm,
        double? scaleConfidence = null,
        double? poseQuality = null,
        double? gridSpacingPx = null,
        double? gridSpacingStdDevPx = null,
        double? homographyInlierRatio = null,
        double? poseReprojectionErrorPx = null,
        bool geometryDerived = false,
        double toleranceMm = 0.2);
}
