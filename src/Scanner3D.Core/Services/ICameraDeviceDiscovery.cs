using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface ICameraDeviceDiscovery
{
    Task<IReadOnlyList<CameraDeviceInfo>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default);
}
