using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class MockCameraModeProvider : ICameraModeProvider
{
    public Task<IReadOnlyList<CameraCaptureMode>> GetSupportedModesAsync(
        string cameraDeviceId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CameraCaptureMode> modes =
        [
            new(1920, 1080, 30, "MJPG"),
            new(1280, 720, 60, "YUY2"),
            new(1280, 720, 30, "YUY2")
        ];

        return Task.FromResult(modes);
    }
}
