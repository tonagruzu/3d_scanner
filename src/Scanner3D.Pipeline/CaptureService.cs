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
        _cameraDeviceDiscovery = cameraDeviceDiscovery ?? new MockCameraDeviceDiscovery();
        _frameCaptureProvider = frameCaptureProvider ?? new MockFrameCaptureProvider();
    }

    public async Task<CaptureResult> CaptureAsync(ScanSession session, CaptureSettings settings, CancellationToken cancellationToken = default)
    {
        var devices = await _cameraDeviceDiscovery.GetAvailableDevicesAsync(cancellationToken);

        var selectedDeviceId = devices
            .Where(device => device.IsAvailable)
            .Select(device => device.DeviceId)
            .FirstOrDefault(deviceId => string.Equals(deviceId, session.CameraDeviceId, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault(device => device.IsAvailable)?.DeviceId
            ?? session.CameraDeviceId;

        var frames = await _frameCaptureProvider.CaptureFramesAsync(selectedDeviceId, settings.TargetFrameCount, cancellationToken);

        var acceptedFrameCount = frames.Count(frame => frame.Accepted);
        var notes = $"Capture settings: lockExposure={settings.LockExposure}, lockWhiteBalance={settings.LockWhiteBalance}, underlay={settings.UnderlayPattern}, lighting={settings.LightingProfile}";

        return new CaptureResult(
            CameraDeviceId: selectedDeviceId,
            CapturedFrameCount: frames.Count,
            AcceptedFrameCount: acceptedFrameCount,
            Frames: frames,
            Notes: notes);
    }
}
