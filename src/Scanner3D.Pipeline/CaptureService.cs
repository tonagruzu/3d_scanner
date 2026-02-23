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

        var requiredAcceptedFrameCount = Math.Clamp(settings.MinimumAcceptedFrameCount, 1, Math.Max(1, settings.TargetFrameCount));
        var maxCaptureAttempts = Math.Max(1, settings.MaxCaptureAttempts);

        FrameCaptureResult? bestFrameCaptureResult = null;
        var bestAcceptedFrameCount = -1;
        var attemptsUsed = 0;

        for (var attempt = 1; attempt <= maxCaptureAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frameCaptureResult = await frameProvider.CaptureFramesAsync(selectedDeviceId, settings, cancellationToken);
            if (!settings.AllowMockFallback && string.Equals(frameCaptureResult.Diagnostics.BackendUsed, "mock", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Capture provider fell back to mock backend, but mock fallback is disabled for this run.");
            }

            attemptsUsed = attempt;
            var acceptedCount = frameCaptureResult.Frames.Count(frame => frame.Accepted);
            if (acceptedCount > bestAcceptedFrameCount)
            {
                bestAcceptedFrameCount = acceptedCount;
                bestFrameCaptureResult = frameCaptureResult;
            }

            if (acceptedCount >= requiredAcceptedFrameCount)
            {
                break;
            }
        }

        bestFrameCaptureResult ??= new FrameCaptureResult(
            Frames: [],
            Diagnostics: new FrameCaptureDiagnostics(
                BackendUsed: "unknown",
                ExposureLockVerified: null,
                WhiteBalanceLockVerified: null,
                TimestampSource: "unknown"));

        var frames = bestFrameCaptureResult.Frames;
        var acceptedFrameCount = frames.Count(frame => frame.Accepted);
        var reliabilityTargetMet = acceptedFrameCount >= requiredAcceptedFrameCount;
        var reliabilityFailureReason = reliabilityTargetMet
            ? null
            : $"Accepted frames {acceptedFrameCount}/{requiredAcceptedFrameCount} after {attemptsUsed}/{maxCaptureAttempts} capture attempts.";

        var notes = $"device={selectedDeviceName}; mode={selectedMode}; backend={bestFrameCaptureResult.Diagnostics.BackendUsed}; lockExposure={settings.LockExposure}; exposureLockVerified={bestFrameCaptureResult.Diagnostics.ExposureLockVerified?.ToString() ?? "unknown"}; lockWhiteBalance={settings.LockWhiteBalance}; whiteBalanceLockVerified={bestFrameCaptureResult.Diagnostics.WhiteBalanceLockVerified?.ToString() ?? "unknown"}; timestampSource={bestFrameCaptureResult.Diagnostics.TimestampSource}; minAccepted={requiredAcceptedFrameCount}; attempts={attemptsUsed}/{maxCaptureAttempts}; underlay={settings.UnderlayPattern}; lighting={settings.LightingProfile}";
        if (!string.IsNullOrWhiteSpace(reliabilityFailureReason))
        {
            notes += $"; reliabilityFailure={reliabilityFailureReason}";
        }

        return new CaptureResult(
            CameraDeviceId: selectedDeviceId,
            SelectedMode: selectedMode,
            CapturedFrameCount: frames.Count,
            AcceptedFrameCount: acceptedFrameCount,
            RequiredAcceptedFrameCount: requiredAcceptedFrameCount,
            CaptureAttemptsUsed: attemptsUsed,
            MaxCaptureAttempts: maxCaptureAttempts,
            ReliabilityTargetMet: reliabilityTargetMet,
            ReliabilityFailureReason: reliabilityFailureReason,
            Frames: frames,
            CaptureBackend: bestFrameCaptureResult.Diagnostics.BackendUsed,
            ExposureLockRequested: settings.LockExposure,
            WhiteBalanceLockRequested: settings.LockWhiteBalance,
            ExposureLockVerified: bestFrameCaptureResult.Diagnostics.ExposureLockVerified,
            WhiteBalanceLockVerified: bestFrameCaptureResult.Diagnostics.WhiteBalanceLockVerified,
            FrameTimestampSource: bestFrameCaptureResult.Diagnostics.TimestampSource,
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
