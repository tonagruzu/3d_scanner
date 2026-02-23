using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class CaptureService : ICaptureService
{
    private readonly ICameraDeviceDiscovery _cameraDeviceDiscovery;
    private readonly IFrameCaptureProvider _frameCaptureProvider;

    public CaptureService(
        ICameraDeviceDiscovery? cameraDeviceDiscovery = null,
        IFrameCaptureProvider? frameCaptureProvider = null)
    {
        _cameraDeviceDiscovery = cameraDeviceDiscovery ?? CreateDefaultDeviceDiscovery();
        _frameCaptureProvider = frameCaptureProvider ?? CreateDefaultFrameCaptureProvider();
    }

    private static ICameraDeviceDiscovery CreateDefaultDeviceDiscovery()
    {
        return OperatingSystem.IsWindows()
            ? new CompositeCameraDeviceDiscovery(new WindowsCameraDeviceDiscovery(), new MockCameraDeviceDiscovery())
            : new MockCameraDeviceDiscovery();
    }

    private static IFrameCaptureProvider CreateDefaultFrameCaptureProvider()
    {
        return OperatingSystem.IsWindows()
            ? new CompositeFrameCaptureProvider(new WindowsFrameCaptureProvider(), new MockFrameCaptureProvider())
            : new MockFrameCaptureProvider();
    }

    public async Task<CaptureResult> CaptureAsync(ScanSession session, CaptureSettings settings, CancellationToken cancellationToken = default)
    {
        var devices = await _cameraDeviceDiscovery.GetAvailableDevicesAsync(cancellationToken);

        var selectedDevice = devices
            .Where(device => device.IsAvailable)
            .FirstOrDefault(device => string.Equals(device.DeviceId, session.CameraDeviceId, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault(device => device.IsAvailable);

        var selectedDeviceId = selectedDevice?.DeviceId ?? session.CameraDeviceId;
        var selectedDeviceName = selectedDevice?.DisplayName ?? "SessionCameraFallback";

        var frames = await _frameCaptureProvider.CaptureFramesAsync(selectedDeviceId, settings.TargetFrameCount, cancellationToken);

        var acceptedFrameCount = frames.Count(frame => frame.Accepted);
        var notes = $"device={selectedDeviceName}; lockExposure={settings.LockExposure}; lockWhiteBalance={settings.LockWhiteBalance}; underlay={settings.UnderlayPattern}; lighting={settings.LightingProfile}";

        return new CaptureResult(
            CameraDeviceId: selectedDeviceId,
            CapturedFrameCount: frames.Count,
            AcceptedFrameCount: acceptedFrameCount,
            Frames: frames,
            Notes: notes);
    }
}
