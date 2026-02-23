using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface IFrameCaptureProvider
{
    Task<IReadOnlyList<CaptureFrame>> CaptureFramesAsync(
        string cameraDeviceId,
        int targetFrameCount,
        CancellationToken cancellationToken = default);
}
