using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class MockFrameCaptureProvider : IFrameCaptureProvider
{
    public async Task<FrameCaptureResult> CaptureFramesAsync(
        string cameraDeviceId,
        CaptureSettings settings,
        CancellationToken cancellationToken = default)
    {
        var frameCount = Math.Max(3, settings.TargetFrameCount);
        var frames = new List<CaptureFrame>(frameCount);

        for (var index = 1; index <= frameCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sharpness = Math.Max(0.6, 0.95 - (index * 0.02));
            var exposure = Math.Max(0.75, 0.92 - ((index % 4) * 0.03));
            var accepted = sharpness >= CaptureQualityThresholds.SharpnessMinForAcceptance
                           && exposure >= CaptureQualityThresholds.ExposureMinForAcceptance;

            frames.Add(new CaptureFrame(
                FrameId: $"{cameraDeviceId}-f-{index:000}",
                CapturedAt: DateTimeOffset.UtcNow.AddMilliseconds(index * 100),
                SourceTimestampMs: index * 100,
                SharpnessScore: sharpness,
                ExposureScore: exposure,
                Accepted: accepted));

            await Task.Delay(35, cancellationToken);
        }

        return new FrameCaptureResult(
            Frames: frames,
            Diagnostics: new FrameCaptureDiagnostics(
                BackendUsed: "mock",
                ExposureLockVerified: null,
                WhiteBalanceLockVerified: null,
                ExposureLockStatus: settings.LockExposure ? LockVerificationStatus.Unsupported : LockVerificationStatus.NotRequested,
                WhiteBalanceLockStatus: settings.LockWhiteBalance ? LockVerificationStatus.Unsupported : LockVerificationStatus.NotRequested,
                TimestampSource: "system_clock_utc"));
    }
}
