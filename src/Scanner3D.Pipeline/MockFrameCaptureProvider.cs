using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class MockFrameCaptureProvider : IFrameCaptureProvider
{
    public Task<IReadOnlyList<CaptureFrame>> CaptureFramesAsync(
        string cameraDeviceId,
        int targetFrameCount,
        CancellationToken cancellationToken = default)
    {
        var frameCount = Math.Max(3, targetFrameCount);
        var frames = new List<CaptureFrame>(frameCount);

        for (var index = 1; index <= frameCount; index++)
        {
            var sharpness = Math.Max(0.6, 0.95 - (index * 0.02));
            var exposure = Math.Max(0.75, 0.92 - ((index % 4) * 0.03));
            var accepted = sharpness >= 0.8 && exposure >= 0.82;

            frames.Add(new CaptureFrame(
                FrameId: $"{cameraDeviceId}-f-{index:000}",
                CapturedAt: DateTimeOffset.UtcNow.AddMilliseconds(index * 100),
                SharpnessScore: sharpness,
                ExposureScore: exposure,
                Accepted: accepted));
        }

        return Task.FromResult<IReadOnlyList<CaptureFrame>>(frames);
    }
}
