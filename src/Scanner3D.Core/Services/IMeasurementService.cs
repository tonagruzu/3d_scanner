using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface IMeasurementService
{
    Task<IReadOnlyList<DimensionMeasurement>> MeasureAsync(
        MeasurementProfile profile,
        CalibrationResult calibration,
        CancellationToken cancellationToken = default);
}
