using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class UnderlayPatternValidator : IUnderlayPatternValidator
{
    public UnderlayVerificationResult Validate(
        string underlayPatternId,
        string detectionMode,
        double expectedBoxSizeMm,
        IReadOnlyList<double> measuredBoxSizesMm,
        double? scaleConfidence = null,
        double? poseQuality = null,
        double toleranceMm = 0.2)
    {
        if (measuredBoxSizesMm.Count == 0)
        {
            return new UnderlayVerificationResult(
                Performed: false,
                UnderlayPatternId: underlayPatternId,
                DetectionMode: detectionMode,
                ExpectedBoxSizeMm: expectedBoxSizeMm,
                MeasuredBoxSizesMm: measuredBoxSizesMm,
                InlierBoxSizesMm: [],
                MeanBoxSizeMm: 0,
                MaxAbsoluteErrorMm: double.MaxValue,
                FitConfidence: 0,
                ScaleConfidence: 0,
                PoseQuality: 0,
                Pass: false,
                Notes: "No measured underlay boxes provided.");
        }

        var inliers = SelectInliers(measuredBoxSizesMm);
        var valuesForScoring = inliers.Count == 0 ? measuredBoxSizesMm : inliers;

        var mean = valuesForScoring.Average();
        var maxAbsError = valuesForScoring.Max(value => Math.Abs(value - expectedBoxSizeMm));
        var fitConfidence = ComputeFitConfidence(measuredBoxSizesMm, valuesForScoring, expectedBoxSizeMm, toleranceMm);
        var resolvedScaleConfidence = Math.Round(Math.Clamp(scaleConfidence ?? fitConfidence, 0.0, 1.0), 3);
        var resolvedPoseQuality = Math.Round(Math.Clamp(poseQuality ?? (fitConfidence * 0.95), 0.0, 1.0), 3);
        var pass = maxAbsError <= toleranceMm;

        return new UnderlayVerificationResult(
            Performed: true,
            UnderlayPatternId: underlayPatternId,
            DetectionMode: detectionMode,
            ExpectedBoxSizeMm: expectedBoxSizeMm,
            MeasuredBoxSizesMm: measuredBoxSizesMm,
            InlierBoxSizesMm: valuesForScoring.ToList(),
            MeanBoxSizeMm: mean,
            MaxAbsoluteErrorMm: maxAbsError,
            FitConfidence: fitConfidence,
            ScaleConfidence: resolvedScaleConfidence,
            PoseQuality: resolvedPoseQuality,
            Pass: pass,
            Notes: pass
                ? "Underlay print scale verification passed."
                : "Underlay print scale verification failed.");
    }

    private static IReadOnlyList<double> SelectInliers(IReadOnlyList<double> samples)
    {
        if (samples.Count < 4)
        {
            return samples;
        }

        var ordered = samples.OrderBy(value => value).ToList();
        var median = ordered.Count % 2 == 1
            ? ordered[ordered.Count / 2]
            : (ordered[(ordered.Count / 2) - 1] + ordered[ordered.Count / 2]) / 2.0;

        var deviations = ordered.Select(value => Math.Abs(value - median)).OrderBy(value => value).ToList();
        var mad = deviations.Count % 2 == 1
            ? deviations[deviations.Count / 2]
            : (deviations[(deviations.Count / 2) - 1] + deviations[deviations.Count / 2]) / 2.0;

        if (mad <= 1e-6)
        {
            return samples;
        }

        var threshold = 3.0 * mad;
        var inliers = samples.Where(value => Math.Abs(value - median) <= threshold).ToList();
        return inliers.Count >= 3 ? inliers : samples;
    }

    private static double ComputeFitConfidence(
        IReadOnlyList<double> allSamples,
        IReadOnlyList<double> inliers,
        double expectedBoxSizeMm,
        double toleranceMm)
    {
        if (inliers.Count == 0 || allSamples.Count == 0)
        {
            return 0;
        }

        var inlierRatio = (double)inliers.Count / allSamples.Count;
        var meanAbsError = inliers.Average(value => Math.Abs(value - expectedBoxSizeMm));
        var variancePenalty = Math.Min(1.0, meanAbsError / Math.Max(toleranceMm, 1e-6));
        var confidence = inlierRatio * (1.0 - variancePenalty);
        return Math.Round(Math.Clamp(confidence, 0.0, 1.0), 3);
    }
}
