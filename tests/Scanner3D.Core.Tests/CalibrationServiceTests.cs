using Scanner3D.Core.Models;
using OpenCvSharp;
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
    public async Task CalibrateAsync_UsesCheckerboardIntrinsics_WhenDetectablePreviewFramesAreProvided()
    {
        var service = new CalibrationService();
        var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "usb-hd-cam-01", "calibration-test");

        var preview1 = CreateCheckerboardPreviewImage();
        var preview2 = CreateCheckerboardPreviewImage(rotationDegrees: 2.0);
        var preview3 = CreateCheckerboardPreviewImage(rotationDegrees: -2.0);

        try
        {
            var capture = BuildCaptureResult(
                new CaptureFrame("f1", DateTimeOffset.UtcNow, 100, 0.95, 0.90, true, preview1),
                new CaptureFrame("f2", DateTimeOffset.UtcNow, 200, 0.93, 0.88, true, preview2),
                new CaptureFrame("f3", DateTimeOffset.UtcNow, 300, 0.92, 0.87, true, preview3));

            var result = await service.CalibrateAsync(session, capture);

            Assert.NotNull(result.IntrinsicCalibration);
            Assert.Contains("checkerboard-derived", result.Notes, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("checkerboard", result.IntrinsicCalibration!.PatternType);
            Assert.Equal(9, result.IntrinsicCalibration.PatternColumns);
            Assert.Equal(6, result.IntrinsicCalibration.PatternRows);
            Assert.True(result.IntrinsicCalibration.CameraMatrix.Count == 9);
            Assert.True(result.IntrinsicCalibration.UsedFrameIds.Count >= 3);
        }
        finally
        {
            DeleteFileIfExists(preview1);
            DeleteFileIfExists(preview2);
            DeleteFileIfExists(preview3);
        }
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

    private static string CreateCheckerboardPreviewImage(double rotationDegrees = 0)
    {
        const int boardColumns = 10;
        const int boardRows = 7;
        const int squareSizePx = 48;

        var width = boardColumns * squareSizePx;
        var height = boardRows * squareSizePx;
        using var image = new Mat(new Size(width, height), MatType.CV_8UC1, Scalar.All(255));

        for (var row = 0; row < boardRows; row++)
        {
            for (var column = 0; column < boardColumns; column++)
            {
                if ((row + column) % 2 == 0)
                {
                    var rect = new Rect(column * squareSizePx, row * squareSizePx, squareSizePx, squareSizePx);
                    Cv2.Rectangle(image, rect, Scalar.All(0), -1);
                }
            }
        }

        Mat output;
        if (Math.Abs(rotationDegrees) > 0.001)
        {
            output = new Mat();
            var center = new Point2f(width / 2f, height / 2f);
            var transform = Cv2.GetRotationMatrix2D(center, rotationDegrees, 1.0);
            Cv2.WarpAffine(image, output, transform, image.Size(), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(255));
            transform.Dispose();
        }
        else
        {
            output = image.Clone();
        }

        var path = Path.Combine(Path.GetTempPath(), $"scanner3d-checker-{Guid.NewGuid():N}.png");
        Cv2.ImWrite(path, output);
        output.Dispose();
        return path;
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
