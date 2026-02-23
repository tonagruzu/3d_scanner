using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class CapturePreflightService
{
    private readonly ICameraDeviceDiscovery _cameraDeviceDiscovery;
    private readonly ICameraModeProvider _cameraModeProvider;

    public CapturePreflightService(
        ICameraDeviceDiscovery? cameraDeviceDiscovery = null,
        ICameraModeProvider? cameraModeProvider = null)
    {
        _cameraDeviceDiscovery = cameraDeviceDiscovery ?? CreateDefaultDeviceDiscovery();
        _cameraModeProvider = cameraModeProvider ?? CreateDefaultModeProvider();
    }

    public async Task<CapturePreflightResult> EvaluateAsync(
        ScanSession session,
        CaptureSettings settings,
        CancellationToken cancellationToken = default)
    {
        var blockingIssues = new List<string>();
        var warnings = new List<string>();

        var devices = await _cameraDeviceDiscovery.GetAvailableDevicesAsync(cancellationToken);
        var selectedDevice = devices
            .Where(device => device.IsAvailable)
            .FirstOrDefault(device => string.Equals(device.DeviceId, session.CameraDeviceId, StringComparison.OrdinalIgnoreCase))
            ?? devices.FirstOrDefault(device => device.IsAvailable);

        if (selectedDevice is null)
        {
            blockingIssues.Add("No available camera device was discovered.");
            var failedSummary = "Preflight failed: no available camera device.";
            return new CapturePreflightResult(
                Pass: false,
                SelectedCamera: null,
                ModeList: [],
                BackendCandidate: "unknown",
                MockFallbackAllowed: settings.AllowMockFallback,
                ExposureLockVerificationSupported: false,
                WhiteBalanceLockVerificationSupported: false,
                TimestampReadinessPass: false,
                BlockingIssues: blockingIssues,
                Warnings: warnings,
                Summary: failedSummary);
        }

        var selectedDeviceId = selectedDevice.DeviceId;
        var selectedCamera = new SelectedCameraInfo(selectedDevice.DeviceId, selectedDevice.DisplayName);
        var backendCandidate = InferBackendCandidate(selectedDeviceId);
        var modeList = (await _cameraModeProvider.GetSupportedModesAsync(selectedDeviceId, cancellationToken)).ToList();

        if (modeList.Count == 0)
        {
            blockingIssues.Add("No supported capture modes were discovered for the selected camera.");
        }

        var exposureVerificationSupported = !string.Equals(backendCandidate, "mock", StringComparison.OrdinalIgnoreCase);
        var whiteBalanceVerificationSupported = !string.Equals(backendCandidate, "mock", StringComparison.OrdinalIgnoreCase);
        var timestampReadinessPass = !string.Equals(backendCandidate, "unknown", StringComparison.OrdinalIgnoreCase);

        if (settings.LockExposure && !exposureVerificationSupported)
        {
            blockingIssues.Add("Exposure lock verification is not supported for the selected backend.");
        }

        if (settings.LockWhiteBalance && !whiteBalanceVerificationSupported)
        {
            blockingIssues.Add("White balance lock verification is not supported for the selected backend.");
        }

        if (!timestampReadinessPass)
        {
            blockingIssues.Add("Frame timestamp source is not known for the selected backend.");
        }

        if (string.Equals(backendCandidate, "mock", StringComparison.OrdinalIgnoreCase))
        {
            if (!settings.AllowMockFallback)
            {
                blockingIssues.Add("Mock fallback backend is not allowed for this run.");
            }
            else
            {
                warnings.Add("Running with mock fallback backend (test mode).");
            }
        }

        if (!string.Equals(selectedDeviceId, session.CameraDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Requested camera '{session.CameraDeviceId}' was unavailable; selected '{selectedDeviceId}' instead.");
        }

        var pass = blockingIssues.Count == 0;
        var summary = pass
            ? "Preflight pass: capture backend and camera capabilities satisfy session requirements."
            : "Preflight failed: one or more capture readiness checks did not pass.";

        return new CapturePreflightResult(
            Pass: pass,
            SelectedCamera: selectedCamera,
            ModeList: modeList,
            BackendCandidate: backendCandidate,
            MockFallbackAllowed: settings.AllowMockFallback,
            ExposureLockVerificationSupported: exposureVerificationSupported,
            WhiteBalanceLockVerificationSupported: whiteBalanceVerificationSupported,
            TimestampReadinessPass: timestampReadinessPass,
            BlockingIssues: blockingIssues,
            Warnings: warnings,
            Summary: summary);
    }

    private static ICameraDeviceDiscovery CreateDefaultDeviceDiscovery()
    {
        return OperatingSystem.IsWindows()
            ? new CompositeCameraDeviceDiscovery(
                new WindowsCameraDeviceDiscovery(),
                new CompositeCameraDeviceDiscovery(new OpenCvCameraDeviceDiscovery(), new MockCameraDeviceDiscovery()))
            : new MockCameraDeviceDiscovery();
    }

    private static ICameraModeProvider CreateDefaultModeProvider()
    {
        return OperatingSystem.IsWindows()
            ? new CompositeCameraModeProvider(
                new WindowsCameraModeProvider(),
                new CompositeCameraModeProvider(new OpenCvCameraModeProvider(), new MockCameraModeProvider()))
            : new MockCameraModeProvider();
    }

    private static string InferBackendCandidate(string cameraDeviceId)
    {
        if (cameraDeviceId.StartsWith("opencv-camera-", StringComparison.OrdinalIgnoreCase)
            || int.TryParse(cameraDeviceId, out _))
        {
            return "opencv";
        }

        if (string.Equals(cameraDeviceId, "bootstrap-device", StringComparison.OrdinalIgnoreCase)
            || cameraDeviceId.StartsWith("usb-hd-cam-", StringComparison.OrdinalIgnoreCase))
        {
            return "mock";
        }

        if (cameraDeviceId.Contains("\\", StringComparison.Ordinal))
        {
            return "windows-dshow";
        }

        return "unknown";
    }
}
