using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class MockCalibrationResidualProvider : ICalibrationResidualProvider
{
    public Task<CalibrationResidualSamples> GetResidualSamplesAsync(string calibrationProfileId, CancellationToken cancellationToken = default)
    {
        var samples = new CalibrationResidualSamples(
            ReprojectionResidualSamplesPx: new List<double> { 0.31, 0.44, 0.49, 0.42, 0.38 },
            ScaleResidualSamplesMm: new List<double> { 0.08, 0.12, 0.10, 0.14, 0.11 });

        return Task.FromResult(samples);
    }
}
