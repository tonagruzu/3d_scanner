using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class CaptureService : ICaptureService
{
    private readonly ICameraDeviceDiscovery _cameraDeviceDiscovery;
    private readonly ICameraModeProvider _cameraModeProvider;
    private readonly IFrameCaptureProvider _frameCaptureProvider;
    private readonly bool _useInjectedProviders;

    public CaptureService(
        ICameraDeviceDiscovery? cameraDeviceDiscovery = null,
        ICameraModeProvider? cameraModeProvider = null,
        IFrameCaptureProvider? frameCaptureProvider = null)
    {
        _cameraDeviceDiscovery = cameraDeviceDiscovery ?? new MockCameraDeviceDiscovery();
        _cameraModeProvider = cameraModeProvider ?? new MockCameraModeProvider();
        _frameCaptureProvider = frameCaptureProvider ?? new MockFrameCaptureProvider();
        _useInjectedProviders = cameraDeviceDiscovery is not null
            || cameraModeProvider is not null
            || frameCaptureProvider is not null;
    }

    public async Task<CaptureResult> CaptureAsync(ScanSession session, CaptureSettings settings, CancellationToken cancellationToken = default)
    {
        var backend = ResolveBackend(settings.PreferredBackend, settings.AllowMockFallback);

        var discovery = _useInjectedProviders ? _cameraDeviceDiscovery : backend.DeviceDiscovery;
        var modeProvider = _useInjectedProviders ? _cameraModeProvider : backend.ModeProvider;
        var frameProvider = _useInjectedProviders ? _frameCaptureProvider : backend.FrameCaptureProvider;

        var devices = await discovery.GetAvailableDevicesAsync(cancellationToken);

        var selectedDevice = devices
            .Where(device => device.IsAvailable)
            .FirstOrDefault(device => string.Equals(device.DeviceId, session.CameraDeviceId, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault(device => device.IsAvailable);

        var selectedDeviceId = selectedDevice?.DeviceId ?? session.CameraDeviceId;
        var selectedDeviceName = selectedDevice?.DisplayName ?? "SessionCameraFallback";

        var supportedModes = await modeProvider.GetSupportedModesAsync(selectedDeviceId, cancellationToken);
        var selectedMode = selectedDevice?.PreferredMode
                           ?? supportedModes.FirstOrDefault()
                           ?? new CameraCaptureMode(1280, 720, 30, "Unknown");

        var frameCaptureResult = await frameProvider.CaptureFramesAsync(selectedDeviceId, settings, cancellationToken);
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

    private static (ICameraDeviceDiscovery DeviceDiscovery, ICameraModeProvider ModeProvider, IFrameCaptureProvider FrameCaptureProvider) ResolveBackend(string? preferredBackend, bool allowMockFallback)
    {
        var normalized = preferredBackend?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized == "windows" || normalized == "windows-dshow")
        {
            if (OperatingSystem.IsWindows())
            {
                return (new WindowsCameraDeviceDiscovery(), new WindowsCameraModeProvider(), new WindowsFrameCaptureProvider());
            }

            if (allowMockFallback)
            {
                return (new MockCameraDeviceDiscovery(), new MockCameraModeProvider(), new MockFrameCaptureProvider());
            }

            throw new InvalidOperationException("Windows capture backend is unavailable on this platform and mock fallback is disabled.");
        }

        if (normalized == "opencv")
        {
            return (new OpenCvCameraDeviceDiscovery(), new OpenCvCameraModeProvider(), new OpenCvFrameCaptureProvider());
        }

        if (normalized == "mock")
        {
            if (!allowMockFallback)
            {
                throw new InvalidOperationException("Mock backend was selected but mock fallback is disabled.");
            }

            return (new MockCameraDeviceDiscovery(), new MockCameraModeProvider(), new MockFrameCaptureProvider());
        }

        throw new InvalidOperationException($"Unsupported capture backend '{preferredBackend}'.");
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
