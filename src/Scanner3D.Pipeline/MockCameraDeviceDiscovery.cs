using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class MockCameraDeviceDiscovery : ICameraDeviceDiscovery
{
    public Task<IReadOnlyList<CameraDeviceInfo>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CameraDeviceInfo> devices =
        [
            new("bootstrap-device", "Bootstrap USB Camera", true),
            new("usb-hd-cam-01", "USB HD Camera #1", true)
        ];

        return Task.FromResult(devices);
    }
}
