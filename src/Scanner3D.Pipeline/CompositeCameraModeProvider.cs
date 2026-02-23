using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class CompositeCameraModeProvider : ICameraModeProvider
{
    private readonly ICameraModeProvider _primary;
    private readonly ICameraModeProvider _fallback;

    public CompositeCameraModeProvider(ICameraModeProvider primary, ICameraModeProvider fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<IReadOnlyList<CameraCaptureMode>> GetSupportedModesAsync(
        string cameraDeviceId,
        CancellationToken cancellationToken = default)
    {
        var primaryModes = await _primary.GetSupportedModesAsync(cameraDeviceId, cancellationToken);
        if (primaryModes.Count > 0)
        {
            return primaryModes;
        }

        return await _fallback.GetSupportedModesAsync(cameraDeviceId, cancellationToken);
    }
}
