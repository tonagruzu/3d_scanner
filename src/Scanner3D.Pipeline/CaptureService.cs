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
            ? new CompositeCameraDeviceDiscovery(
                new WindowsCameraDeviceDiscovery(),
                new CompositeCameraDeviceDiscovery(new OpenCvCameraDeviceDiscovery(), new MockCameraDeviceDiscovery()))
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
            ? new CompositeCameraModeProvider(
                new WindowsCameraModeProvider(),
                new CompositeCameraModeProvider(new OpenCvCameraModeProvider(), new MockCameraModeProvider()))
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

        var frameCaptureResult = await _frameCaptureProvider.CaptureFramesAsync(selectedDeviceId, settings, cancellationToken);
        var frames = frameCaptureResult.Frames;

        if (!settings.AllowMockFallback && string.Equals(frameCaptureResult.Diagnostics.BackendUsed, "mock", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Capture provider fell back to mock backend, but mock fallback is disabled for this run.");
        }

        var acceptedFrameCount = frames.Count(frame => frame.Accepted);
        var notes = $"device={selectedDeviceName}; mode={selectedMode}; backend={frameCaptureResult.Diagnostics.BackendUsed}; lockExposure={settings.LockExposure}; exposureLockVerified={frameCaptureResult.Diagnostics.ExposureLockVerified?.ToString() ?? "unknown"}; lockWhiteBalance={settings.LockWhiteBalance}; whiteBalanceLockVerified={frameCaptureResult.Diagnostics.WhiteBalanceLockVerified?.ToString() ?? "unknown"}; timestampSource={frameCaptureResult.Diagnostics.TimestampSource}; underlay={settings.UnderlayPattern}; lighting={settings.LightingProfile}";

        return new CaptureResult(
            CameraDeviceId: selectedDeviceId,
            SelectedMode: selectedMode,
            CapturedFrameCount: frames.Count,
            AcceptedFrameCount: acceptedFrameCount,
            Frames: frames,
            CaptureBackend: frameCaptureResult.Diagnostics.BackendUsed,
            ExposureLockRequested: settings.LockExposure,
            WhiteBalanceLockRequested: settings.LockWhiteBalance,
            ExposureLockVerified: frameCaptureResult.Diagnostics.ExposureLockVerified,
            WhiteBalanceLockVerified: frameCaptureResult.Diagnostics.WhiteBalanceLockVerified,
            FrameTimestampSource: frameCaptureResult.Diagnostics.TimestampSource,
            FrameTimestampsMonotonic: AreTimestampsMonotonic(frames),
            Notes: notes);
    }

    private static bool AreTimestampsMonotonic(IReadOnlyList<CaptureFrame> frames)
    {
        for (var index = 1; index < frames.Count; index++)
        {
            if (frames[index].CapturedAt < frames[index - 1].CapturedAt)
            {
                return false;
            }
        }

        return true;
    }
}
