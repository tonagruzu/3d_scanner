using Scanner3D.Core.Models;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class MeasurementServiceTests
{
    [Fact]
    public async Task MeasureAsync_ReturnsMeasurementsForAllReferences()
    {
        var service = new MeasurementService();
        var calibration = new CalibrationResult(
            CalibrationProfileId: "calib-test",
            CalibratedAt: DateTimeOffset.UtcNow,
            ReprojectionErrorPx: 0.4,
            ScaleErrorMm: 0.12,
            IsWithinTolerance: true,
            Notes: "test");

        var profile = new MeasurementProfile(
            References:
            [
                new DimensionReference("Width", 44),
                new DimensionReference("Height", 27),
                new DimensionReference("Depth", 19)
            ],
            ProfileName: "test-profile");

        var measurements = await service.MeasureAsync(profile, calibration);

        Assert.Equal(3, measurements.Count);
        Assert.All(measurements, measurement => Assert.True(measurement.AbsoluteErrorMm >= 0));
    }

    [Fact]
    public async Task MeasureAsync_UsesCalibrationScaleErrorAsSignal()
    {
        var service = new MeasurementService();
        var lowErrorCalibration = new CalibrationResult("low", DateTimeOffset.UtcNow, 0.3, 0.06, true, "");
        var highErrorCalibration = new CalibrationResult("high", DateTimeOffset.UtcNow, 0.3, 0.18, true, "");

        var profile = new MeasurementProfile(
            References: [ new DimensionReference("Width", 50) ],
            ProfileName: "single-dimension");

        var lowResult = await service.MeasureAsync(profile, lowErrorCalibration);
        var highResult = await service.MeasureAsync(profile, highErrorCalibration);

        Assert.True(highResult[0].AbsoluteErrorMm > lowResult[0].AbsoluteErrorMm);
    }
}
