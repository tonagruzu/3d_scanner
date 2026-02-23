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
        double toleranceMm = 0.2);
}
