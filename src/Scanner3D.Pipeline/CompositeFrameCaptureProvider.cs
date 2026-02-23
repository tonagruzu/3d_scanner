using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class CompositeFrameCaptureProvider : IFrameCaptureProvider
{
    private readonly IFrameCaptureProvider _primary;
    private readonly IFrameCaptureProvider _fallback;

    public CompositeFrameCaptureProvider(IFrameCaptureProvider primary, IFrameCaptureProvider fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<IReadOnlyList<CaptureFrame>> CaptureFramesAsync(
        string cameraDeviceId,
        int targetFrameCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var primaryFrames = await _primary.CaptureFramesAsync(cameraDeviceId, targetFrameCount, cancellationToken);
            if (primaryFrames.Count > 0)
            {
                return primaryFrames;
            }
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
        }

        return await _fallback.CaptureFramesAsync(cameraDeviceId, targetFrameCount, cancellationToken);
    }
}
