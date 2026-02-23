using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class UnderlayPatternValidator : IUnderlayPatternValidator
{
    public UnderlayVerificationResult Validate(
        string underlayPatternId,
        double expectedBoxSizeMm,
        IReadOnlyList<double> measuredBoxSizesMm,
        double toleranceMm = 0.2)
    {
        if (measuredBoxSizesMm.Count == 0)
        {
            return new UnderlayVerificationResult(
                Performed: false,
                UnderlayPatternId: underlayPatternId,
                ExpectedBoxSizeMm: expectedBoxSizeMm,
                MeasuredBoxSizesMm: measuredBoxSizesMm,
                MeanBoxSizeMm: 0,
                MaxAbsoluteErrorMm: double.MaxValue,
                Pass: false,
                Notes: "No measured underlay boxes provided.");
        }

        var mean = measuredBoxSizesMm.Average();
        var maxAbsError = measuredBoxSizesMm.Max(value => Math.Abs(value - expectedBoxSizeMm));
        var pass = maxAbsError <= toleranceMm;

        return new UnderlayVerificationResult(
            Performed: true,
            UnderlayPatternId: underlayPatternId,
            ExpectedBoxSizeMm: expectedBoxSizeMm,
            MeasuredBoxSizesMm: measuredBoxSizesMm,
            MeanBoxSizeMm: mean,
            MaxAbsoluteErrorMm: maxAbsError,
            Pass: pass,
            Notes: pass
                ? "Underlay print scale verification passed."
                : "Underlay print scale verification failed.");
    }
}
