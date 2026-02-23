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

    [Fact]
    public async Task CalibrateAsync_UsesCaptureFrames_WhenProvided()
    {
        var service = new CalibrationService();
        var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "usb-hd-cam-01", "calibration-test");
        var capture = BuildCaptureResult(
            new CaptureFrame("f1", DateTimeOffset.UtcNow, 100, 0.95, 0.90, true),
            new CaptureFrame("f2", DateTimeOffset.UtcNow, 200, 0.92, 0.88, true),
            new CaptureFrame("f3", DateTimeOffset.UtcNow, 300, 0.90, 0.86, true));

        var result = await service.CalibrateAsync(session, capture);

        Assert.Contains("frame-derived", result.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.ReprojectionErrorPx > 0);
        Assert.True(result.ScaleErrorMm > 0);
    }

    [Fact]
    public async Task FrameBasedResidualProvider_UsesCaptureFrames_WhenAvailable()
    {
        var provider = new FrameBasedCalibrationResidualProvider();
        var capture = BuildCaptureResult(
            new CaptureFrame("f1", DateTimeOffset.UtcNow, 100, 0.80, 0.75, true),
            new CaptureFrame("f2", DateTimeOffset.UtcNow, 200, 0.78, 0.70, true),
            new CaptureFrame("f3", DateTimeOffset.UtcNow, 300, 0.76, 0.74, true));

        var samples = await provider.GetResidualSamplesAsync("calib-frame", capture);

        Assert.True(samples.ReprojectionResidualSamplesPx.Count >= 3);
        Assert.True(samples.ScaleResidualSamplesMm.Count >= 3);
        Assert.All(samples.ReprojectionResidualSamplesPx, value => Assert.InRange(value, 0.05, 1.5));
        Assert.All(samples.ScaleResidualSamplesMm, value => Assert.InRange(value, 0.01, 0.6));
    }

    private static CaptureResult BuildCaptureResult(params CaptureFrame[] frames)
    {
        var accepted = frames.Count(frame => frame.Accepted);
        return new CaptureResult(
            CameraDeviceId: "test-cam",
            SelectedMode: new CameraCaptureMode(1280, 720, 30, "YUY2"),
            CapturedFrameCount: frames.Length,
            AcceptedFrameCount: accepted,
            RequiredAcceptedFrameCount: 1,
            CaptureAttemptsUsed: 1,
            MaxCaptureAttempts: 1,
            ReliabilityTargetMet: accepted > 0,
            ReliabilityFailureReason: accepted > 0 ? null : "no accepted",
            Frames: frames,
            CaptureBackend: "test-double",
            ExposureLockRequested: true,
            WhiteBalanceLockRequested: true,
            ExposureLockVerified: true,
            WhiteBalanceLockVerified: true,
            ExposureLockStatus: LockVerificationStatus.Verified,
            WhiteBalanceLockStatus: LockVerificationStatus.Verified,
            FrameTimestampSource: "system_clock_utc",
            FrameTimestampsMonotonic: true,
            Notes: "test");
    }
}
