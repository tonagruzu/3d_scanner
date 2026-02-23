using System.Runtime.Versioning;
using OpenCvSharp;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

[SupportedOSPlatform("windows")]
public sealed class WindowsFrameCaptureProvider : IFrameCaptureProvider
{
    private const int ProbeLimit = 6;
    private const double ManualExposureValue = 0.25;
    private const double ManualWhiteBalanceValue = 0.0;

    public WindowsFrameCaptureProvider()
    {
    }

    public async Task<FrameCaptureResult> CaptureFramesAsync(
        string cameraDeviceId,
        CaptureSettings settings,
        CancellationToken cancellationToken = default)
    {
        var cameraIndex = ResolveCameraIndex(cameraDeviceId);
        if (cameraIndex is null)
        {
            return EmptyResult();
        }

        using var capture = new VideoCapture(cameraIndex.Value, VideoCaptureAPIs.DSHOW);
        if (!capture.IsOpened())
        {
            return EmptyResult();
        }

        var exposureLockVerified = TryVerifyLock(capture, VideoCaptureProperties.AutoExposure, ManualExposureValue, settings.LockExposure);
        var whiteBalanceLockVerified = TryVerifyLock(capture, VideoCaptureProperties.WhiteBalanceBlueU, ManualWhiteBalanceValue, settings.LockWhiteBalance);

        var frameCount = Math.Max(3, settings.TargetFrameCount);
        var frames = new List<CaptureFrame>(frameCount);

        for (var index = 1; index <= frameCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var frame = new Mat();
            var ok = capture.Read(frame) && !frame.Empty();
            if (!ok)
            {
                await Task.Delay(10, cancellationToken);
                continue;
            }

            var sharpness = EvaluateSharpnessScore(frame);
            var exposure = EvaluateExposureScore(frame);
            var accepted = sharpness >= CaptureQualityThresholds.SharpnessMinForAcceptance
                           && exposure >= CaptureQualityThresholds.ExposureMinForAcceptance;
            var frameId = $"win-cam-{cameraIndex.Value}-f-{index:000}";
            var previewImagePath = SavePreviewFrame(frame, frameId);

            frames.Add(new CaptureFrame(
                FrameId: frameId,
                CapturedAt: DateTimeOffset.UtcNow.AddMilliseconds(index * 80),
                SharpnessScore: sharpness,
                ExposureScore: exposure,
                Accepted: accepted,
                PreviewImagePath: previewImagePath));

            await Task.Delay(20, cancellationToken);
        }

        return new FrameCaptureResult(
            Frames: frames,
            Diagnostics: new FrameCaptureDiagnostics(
                BackendUsed: "windows-dshow",
                ExposureLockVerified: exposureLockVerified,
                WhiteBalanceLockVerified: whiteBalanceLockVerified,
                TimestampSource: "system_clock_utc"));
    }

    private static FrameCaptureResult EmptyResult()
    {
        return new FrameCaptureResult(
            Frames: [],
            Diagnostics: new FrameCaptureDiagnostics(
                BackendUsed: "windows-dshow",
                ExposureLockVerified: null,
                WhiteBalanceLockVerified: null,
                TimestampSource: "system_clock_utc"));
    }

    private static int? ResolveCameraIndex(string cameraDeviceId)
    {
        if (cameraDeviceId.StartsWith("opencv-camera-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(cameraDeviceId["opencv-camera-".Length..], out var prefixedIndex)
            && prefixedIndex >= 0 && prefixedIndex < ProbeLimit)
        {
            return prefixedIndex;
        }

        if (int.TryParse(cameraDeviceId, out var directIndex)
            && directIndex >= 0 && directIndex < ProbeLimit)
        {
            return directIndex;
        }

        for (var index = 0; index < ProbeLimit; index++)
        {
            using var capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
            if (capture.IsOpened())
            {
                return index;
            }
        }

        return null;
    }

    private static double EvaluateSharpnessScore(Mat frame)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

        using var laplacian = new Mat();
        Cv2.Laplacian(gray, laplacian, MatType.CV_64F);

        Cv2.MeanStdDev(laplacian, out _, out var stddev);
        var variance = stddev.Val0 * stddev.Val0;
        return Math.Clamp(variance / 1000.0, 0.0, 1.0);
    }

    private static double EvaluateExposureScore(Mat frame)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        var mean = Cv2.Mean(gray).Val0;

        var distanceFromMid = Math.Abs(mean - 127.5);
        var normalized = 1.0 - (distanceFromMid / 127.5);
        return Math.Clamp(normalized, 0.0, 1.0);
    }

    private static bool? TryVerifyLock(VideoCapture capture, VideoCaptureProperties property, double targetValue, bool lockRequested)
    {
        if (!lockRequested)
        {
            return null;
        }

        try
        {
            var setSucceeded = capture.Set(property, targetValue);
            var current = capture.Get(property);
            if (double.IsNaN(current) || double.IsInfinity(current))
            {
                return null;
            }

            if (!setSucceeded)
            {
                return false;
            }

            return Math.Abs(current - targetValue) < 0.4;
        }
        catch
        {
            return null;
        }
    }

    private static string? SavePreviewFrame(Mat frame, string frameId)
    {
        try
        {
            var previewDirectory = Path.Combine(Path.GetTempPath(), "scanner3d-preview");
            Directory.CreateDirectory(previewDirectory);

            var filePath = Path.Combine(previewDirectory, $"{frameId}-{Guid.NewGuid():N}.jpg");
            Cv2.ImWrite(filePath, frame);
            return filePath;
        }
        catch
        {
            return null;
        }
    }
}
