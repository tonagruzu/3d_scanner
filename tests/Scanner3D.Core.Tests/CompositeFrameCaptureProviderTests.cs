using Scanner3D.Core.Models;
using Scanner3D.Core.Services;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class CompositeFrameCaptureProviderTests
{
    [Fact]
    public async Task CaptureFramesAsync_UsesPrimaryWhenPrimaryReturnsFrames()
    {
        var primaryFrames = new List<CaptureFrame>
        {
            new("primary-f-001", DateTimeOffset.UtcNow, 100, 0.9, 0.9, true)
        };

        var provider = new CompositeFrameCaptureProvider(
            new StaticFrameProvider(primaryFrames),
            new StaticFrameProvider([]));

        var result = await provider.CaptureFramesAsync("cam-1", new CaptureSettings(3, true, true, "grid", "diffuse"));
        var frames = result.Frames;

        Assert.Single(frames);
        Assert.Equal("primary-f-001", frames[0].FrameId);
    }

    [Fact]
    public async Task CaptureFramesAsync_FallsBackWhenPrimaryReturnsNoFrames()
    {
        var fallbackFrames = new List<CaptureFrame>
        {
            new("fallback-f-001", DateTimeOffset.UtcNow, 100, 0.85, 0.88, true)
        };

        var provider = new CompositeFrameCaptureProvider(
            new StaticFrameProvider([]),
            new StaticFrameProvider(fallbackFrames));

        var result = await provider.CaptureFramesAsync("cam-1", new CaptureSettings(3, true, true, "grid", "diffuse"));
        var frames = result.Frames;

        Assert.Single(frames);
        Assert.Equal("fallback-f-001", frames[0].FrameId);
    }

    private sealed class StaticFrameProvider : IFrameCaptureProvider
    {
        private readonly IReadOnlyList<CaptureFrame> _frames;

        public StaticFrameProvider(IReadOnlyList<CaptureFrame> frames)
        {
            _frames = frames;
        }

        public Task<FrameCaptureResult> CaptureFramesAsync(
            string cameraDeviceId,
            CaptureSettings settings,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new FrameCaptureResult(
                Frames: _frames,
                Diagnostics: new FrameCaptureDiagnostics(
                    BackendUsed: "test-double",
                    ExposureLockVerified: true,
                    WhiteBalanceLockVerified: true,
                    ExposureLockStatus: LockVerificationStatus.Verified,
                    WhiteBalanceLockStatus: LockVerificationStatus.Verified,
                    TimestampSource: "system_clock_utc")));
        }
    }
}
