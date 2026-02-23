using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class CaptureService : ICaptureService
{
    private readonly ICameraDeviceDiscovery _cameraDeviceDiscovery;
    private readonly ICameraModeProvider _cameraModeProvider;
    private readonly IFrameCaptureProvider _frameCaptureProvider;

    public CaptureService(
        ICameraDeviceDiscovery? cameraDeviceDiscovery = null,
        ICameraModeProvider? cameraModeProvider = null,
        IFrameCaptureProvider? frameCaptureProvider = null)
    {
        _cameraDeviceDiscovery = cameraDeviceDiscovery ?? CreateDefaultDeviceDiscovery();
        _cameraModeProvider = cameraModeProvider ?? CreateDefaultModeProvider();
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

    private static ICameraModeProvider CreateDefaultModeProvider()
    {
        return OperatingSystem.IsWindows()
            ? new CompositeCameraModeProvider(new WindowsCameraModeProvider(), new MockCameraModeProvider())
            : new MockCameraModeProvider();
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

        var supportedModes = await _cameraModeProvider.GetSupportedModesAsync(selectedDeviceId, cancellationToken);
        var selectedMode = selectedDevice?.PreferredMode
                           ?? supportedModes.FirstOrDefault()
                           ?? new CameraCaptureMode(1280, 720, 30, "Unknown");

        var frames = await _frameCaptureProvider.CaptureFramesAsync(selectedDeviceId, settings.TargetFrameCount, cancellationToken);

        var acceptedFrameCount = frames.Count(frame => frame.Accepted);
        var notes = $"device={selectedDeviceName}; mode={selectedMode}; lockExposure={settings.LockExposure}; lockWhiteBalance={settings.LockWhiteBalance}; underlay={settings.UnderlayPattern}; lighting={settings.LightingProfile}";

        return new CaptureResult(
            CameraDeviceId: selectedDeviceId,
            SelectedMode: selectedMode,
            CapturedFrameCount: frames.Count,
            AcceptedFrameCount: acceptedFrameCount,
            Frames: frames,
            Notes: notes);
    }
}
