using Scanner3D.Core.Models;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class CalibrationServiceTests
{
    [Fact]
    public async Task CalibrateAsync_ReturnsWithinToleranceResult()
    {
        var service = new CalibrationService();
        var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "usb-hd-cam-01", "calibration-test");

        var result = await service.CalibrateAsync(session);

        Assert.True(result.IsWithinTolerance);
        Assert.True(result.ReprojectionErrorPx <= 0.5);
        Assert.True(result.ScaleErrorMm <= 0.2);
        Assert.StartsWith("calib-", result.CalibrationProfileId);
    }

    [Fact]
    public async Task ResidualProvider_ReturnsSampleSets()
    {
        var provider = new MockCalibrationResidualProvider();

        var samples = await provider.GetResidualSamplesAsync("calib-sample");

        Assert.NotEmpty(samples.ReprojectionResidualSamplesPx);
        Assert.NotEmpty(samples.ScaleResidualSamplesMm);
        Assert.True(samples.ReprojectionResidualSamplesPx.Count >= 3);
        Assert.True(samples.ScaleResidualSamplesMm.Count >= 3);
    }
}
