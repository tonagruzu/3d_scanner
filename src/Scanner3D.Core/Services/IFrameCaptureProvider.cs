using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface IFrameCaptureProvider
{
    Task<FrameCaptureResult> CaptureFramesAsync(
        string cameraDeviceId,
        CaptureSettings settings,
        CancellationToken cancellationToken = default);
}
