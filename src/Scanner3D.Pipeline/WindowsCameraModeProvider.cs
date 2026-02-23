using System.Runtime.Versioning;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

[SupportedOSPlatform("windows")]
public sealed class WindowsCameraModeProvider : ICameraModeProvider
{
    public Task<IReadOnlyList<CameraCaptureMode>> GetSupportedModesAsync(
        string cameraDeviceId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CameraCaptureMode> modes =
        [
            new(1920, 1080, 30, "Unknown"),
            new(1280, 720, 30, "Unknown")
        ];

        return Task.FromResult(modes);
    }
}
