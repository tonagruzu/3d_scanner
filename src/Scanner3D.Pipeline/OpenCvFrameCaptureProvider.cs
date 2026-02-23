using OpenCvSharp;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class OpenCvFrameCaptureProvider : IFrameCaptureProvider
{
    private const int ProbeLimit = 6;

    public async Task<FrameCaptureResult> CaptureFramesAsync(
        string cameraDeviceId,
        CaptureSettings settings,
        CancellationToken cancellationToken = default)
    {
        var index = ParseIndex(cameraDeviceId);
        if (index is null)
        {
            return EmptyResult();
        }

        using var capture = new VideoCapture(index.Value, VideoCaptureAPIs.DSHOW);
        if (!capture.IsOpened())
        {
            return EmptyResult();
        }

        var frameCount = Math.Max(3, settings.TargetFrameCount);
        var frames = new List<CaptureFrame>(frameCount);

        for (var frameIndex = 1; frameIndex <= frameCount; frameIndex++)
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
            var frameId = $"opencv-cam-{index.Value}-f-{frameIndex:000}";
            var previewImagePath = SavePreviewFrame(frame, frameId);

            frames.Add(new CaptureFrame(
                FrameId: frameId,
                CapturedAt: DateTimeOffset.UtcNow.AddMilliseconds(frameIndex * 80),
                SharpnessScore: sharpness,
                ExposureScore: exposure,
                Accepted: accepted,
                PreviewImagePath: previewImagePath));

            await Task.Delay(20, cancellationToken);
        }

        return new FrameCaptureResult(
            Frames: frames,
            Diagnostics: new FrameCaptureDiagnostics(
                BackendUsed: "opencv",
                ExposureLockVerified: null,
                WhiteBalanceLockVerified: null,
                TimestampSource: "system_clock_utc"));
    }

    private static FrameCaptureResult EmptyResult()
    {
        return new FrameCaptureResult(
            Frames: [],
            Diagnostics: new FrameCaptureDiagnostics(
                BackendUsed: "opencv",
                ExposureLockVerified: null,
                WhiteBalanceLockVerified: null,
                TimestampSource: "system_clock_utc"));
    }

    private static int? ParseIndex(string cameraDeviceId)
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
