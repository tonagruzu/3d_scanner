using OpenCvSharp;
using Scanner3D.Core.Models;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class UnderlayBoxSizeEstimatorTests
{
    [Fact]
    public void EstimateMeasuredBoxSizesMm_UsesPreviewImages_WhenAvailable()
    {
        var estimator = new UnderlayBoxSizeEstimator();
        var previewPath = CreateGridPreviewImage();

        try
        {
            var capture = BuildCaptureResult(
                new CaptureFrame("f-001", DateTimeOffset.UtcNow, 100, 0.92, 0.84, true, previewPath),
                new CaptureFrame("f-002", DateTimeOffset.UtcNow, 200, 0.90, 0.82, true, previewPath),
                new CaptureFrame("f-003", DateTimeOffset.UtcNow, 300, 0.91, 0.85, true, previewPath));

            var estimate = estimator.EstimateMeasuredBoxSizesMm(capture, expectedBoxSizeMm: 10.0, targetSamples: 5);

            Assert.Equal("preview-image", estimate.DetectionMode);
            Assert.True(estimate.MeasuredBoxSizesMm.Count >= 3);
            Assert.All(estimate.MeasuredBoxSizesMm, value => Assert.InRange(value, 9.82, 10.18));
            Assert.InRange(estimate.ScaleConfidence, 0.0, 1.0);
            Assert.InRange(estimate.PoseQuality, 0.0, 1.0);
        }
        finally
        {
            if (File.Exists(previewPath))
            {
                File.Delete(previewPath);
            }
        }
    }

    [Fact]
    public void EstimateMeasuredBoxSizesMm_FallsBackToFrameQuality_WhenPreviewMissing()
    {
        var estimator = new UnderlayBoxSizeEstimator();
        var capture = BuildCaptureResult(
            new CaptureFrame("f-001", DateTimeOffset.UtcNow, 100, 0.88, 0.80, true),
            new CaptureFrame("f-002", DateTimeOffset.UtcNow, 200, 0.86, 0.79, true),
            new CaptureFrame("f-003", DateTimeOffset.UtcNow, 300, 0.87, 0.81, true));

        var estimate = estimator.EstimateMeasuredBoxSizesMm(capture, expectedBoxSizeMm: 10.0, targetSamples: 5);

        Assert.Equal("frame-quality-fallback", estimate.DetectionMode);
        Assert.True(estimate.MeasuredBoxSizesMm.Count >= 3);
        Assert.All(estimate.MeasuredBoxSizesMm, value => Assert.InRange(value, 9.84, 10.16));
        Assert.InRange(estimate.ScaleConfidence, 0.0, 1.0);
        Assert.InRange(estimate.PoseQuality, 0.0, 1.0);
    }

    private static string CreateGridPreviewImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"scanner3d-grid-{Guid.NewGuid():N}.png");
        using var image = new Mat(new Size(640, 480), MatType.CV_8UC3, Scalar.All(255));

        for (var x = 40; x < image.Width; x += 40)
        {
            Cv2.Line(image, new Point(x, 0), new Point(x, image.Height - 1), new Scalar(60, 60, 60), 1);
        }

        for (var y = 40; y < image.Height; y += 40)
        {
            Cv2.Line(image, new Point(0, y), new Point(image.Width - 1, y), new Scalar(60, 60, 60), 1);
        }

        Cv2.ImWrite(path, image);
        return path;
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
            ReliabilityTargetMet: true,
            ReliabilityFailureReason: null,
            Frames: frames,
            CaptureBackend: "test-double",
            ExposureLockRequested: false,
            WhiteBalanceLockRequested: false,
            ExposureLockVerified: null,
            WhiteBalanceLockVerified: null,
            ExposureLockStatus: LockVerificationStatus.NotRequested,
            WhiteBalanceLockStatus: LockVerificationStatus.NotRequested,
            FrameTimestampSource: "system_clock_utc",
            FrameTimestampsMonotonic: true,
            Notes: "underlay-test");
    }
}
