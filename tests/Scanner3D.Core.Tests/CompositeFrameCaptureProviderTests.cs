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
            new("primary-f-001", DateTimeOffset.UtcNow, 0.9, 0.9, true)
        };

        var provider = new CompositeFrameCaptureProvider(
            new StaticFrameProvider(primaryFrames),
            new StaticFrameProvider([]));

        var frames = await provider.CaptureFramesAsync("cam-1", 3);

        Assert.Single(frames);
        Assert.Equal("primary-f-001", frames[0].FrameId);
    }

    [Fact]
    public async Task CaptureFramesAsync_FallsBackWhenPrimaryReturnsNoFrames()
    {
        var fallbackFrames = new List<CaptureFrame>
        {
            new("fallback-f-001", DateTimeOffset.UtcNow, 0.85, 0.88, true)
        };

        var provider = new CompositeFrameCaptureProvider(
            new StaticFrameProvider([]),
            new StaticFrameProvider(fallbackFrames));

        var frames = await provider.CaptureFramesAsync("cam-1", 3);

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

        public Task<IReadOnlyList<CaptureFrame>> CaptureFramesAsync(
            string cameraDeviceId,
            int targetFrameCount,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_frames);
        }
    }
}
