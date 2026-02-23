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

    public async Task<FrameCaptureResult> CaptureFramesAsync(
        string cameraDeviceId,
        CaptureSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var primaryFrames = await _primary.CaptureFramesAsync(cameraDeviceId, settings, cancellationToken);
            if (primaryFrames.Frames.Count > 0)
            {
                return primaryFrames;
            }
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
        }

        return await _fallback.CaptureFramesAsync(cameraDeviceId, settings, cancellationToken);
    }
}
