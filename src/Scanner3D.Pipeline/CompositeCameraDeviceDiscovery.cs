using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class CompositeCameraDeviceDiscovery : ICameraDeviceDiscovery
{
    private readonly ICameraDeviceDiscovery _primary;
    private readonly ICameraDeviceDiscovery _fallback;

    public CompositeCameraDeviceDiscovery(ICameraDeviceDiscovery primary, ICameraDeviceDiscovery fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<IReadOnlyList<CameraDeviceInfo>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        var primaryDevices = await _primary.GetAvailableDevicesAsync(cancellationToken);
        if (primaryDevices.Count > 0)
        {
            return primaryDevices;
        }

        return await _fallback.GetAvailableDevicesAsync(cancellationToken);
    }
}
