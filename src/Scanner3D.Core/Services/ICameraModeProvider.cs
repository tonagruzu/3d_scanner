using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface ICameraModeProvider
{
    Task<IReadOnlyList<CameraCaptureMode>> GetSupportedModesAsync(
        string cameraDeviceId,
        CancellationToken cancellationToken = default);
}
