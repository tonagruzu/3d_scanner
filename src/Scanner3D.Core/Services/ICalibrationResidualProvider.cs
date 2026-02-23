using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface ICalibrationResidualProvider
{
    Task<CalibrationResidualSamples> GetResidualSamplesAsync(string calibrationProfileId, CancellationToken cancellationToken = default);
}
