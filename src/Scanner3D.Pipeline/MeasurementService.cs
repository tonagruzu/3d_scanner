using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class MeasurementService : IMeasurementService
{
    private static readonly double[] ErrorMultipliers = [-1.8, 2.2, -1.1, 1.4, -0.9, 0.7];

    public Task<IReadOnlyList<DimensionMeasurement>> MeasureAsync(
        MeasurementProfile profile,
        CalibrationResult calibration,
        CancellationToken cancellationToken = default)
    {
        var scaleFactor = Math.Max(0.05, calibration.ScaleErrorMm);
        var measurements = new List<DimensionMeasurement>(profile.References.Count);

        for (var i = 0; i < profile.References.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reference = profile.References[i];
            var multiplier = ErrorMultipliers[i % ErrorMultipliers.Length];
            var delta = Math.Round(multiplier * scaleFactor, 3);
            var measured = Math.Round(reference.ReferenceMm + delta, 3);
            var absoluteError = Math.Round(Math.Abs(reference.ReferenceMm - measured), 3);

            measurements.Add(new DimensionMeasurement(
                Name: reference.Name,
                ReferenceMm: reference.ReferenceMm,
                MeasuredMm: measured,
                AbsoluteErrorMm: absoluteError));
        }

        return Task.FromResult<IReadOnlyList<DimensionMeasurement>>(measurements);
    }
}
