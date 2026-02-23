using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class CapturePreflightService
{
    private readonly ICameraDeviceDiscovery _cameraDeviceDiscovery;
    private readonly ICameraModeProvider _cameraModeProvider;
    private readonly bool _useInjectedProviders;

    public CapturePreflightService(
        ICameraDeviceDiscovery? cameraDeviceDiscovery = null,
        ICameraModeProvider? cameraModeProvider = null)
    {
        _cameraDeviceDiscovery = cameraDeviceDiscovery ?? new MockCameraDeviceDiscovery();
        _cameraModeProvider = cameraModeProvider ?? new MockCameraModeProvider();
        _useInjectedProviders = cameraDeviceDiscovery is not null || cameraModeProvider is not null;
    }

    public async Task<CapturePreflightResult> EvaluateAsync(
        ScanSession session,
        CaptureSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (_useInjectedProviders)
        {
            return await EvaluateUsingInjectedProvidersAsync(session, settings, cancellationToken);
        }

        var candidateBackends = BuildCandidateBackends(settings);
        var backendSnapshots = new List<(string BackendName, ICameraDeviceDiscovery Discovery, ICameraModeProvider ModeProvider, IReadOnlyList<CameraDeviceInfo> Devices)>();
        foreach (var backend in candidateBackends)
        {
            var devicesForBackend = await backend.DeviceDiscovery.GetAvailableDevicesAsync(cancellationToken);
            backendSnapshots.Add((backend.BackendName, backend.DeviceDiscovery, backend.ModeProvider, devicesForBackend));
        }

        var selectedSnapshot = backendSnapshots
            .Select(snapshot => new
            {
                snapshot.BackendName,
                snapshot.ModeProvider,
                SelectedDevice = snapshot.Devices
                    .Where(device => device.IsAvailable)
                    .FirstOrDefault(device => string.Equals(device.DeviceId, session.CameraDeviceId, StringComparison.OrdinalIgnoreCase))
                    ?? snapshot.Devices.FirstOrDefault(device => device.IsAvailable)
            })
            .FirstOrDefault(item => item.SelectedDevice is not null);

        var blockingIssues = new List<string>();
        var warnings = new List<string>();

        if (selectedSnapshot?.SelectedDevice is null)
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
                ExposureLockCapabilityStatus: LockVerificationStatus.Unknown,
                WhiteBalanceLockCapabilityStatus: LockVerificationStatus.Unknown,
                TimestampReadinessPass: false,
                BlockingIssues: blockingIssues,
                Warnings: warnings,
                Summary: failedSummary);
        }

        var selectedDevice = selectedSnapshot.SelectedDevice;
        var selectedDeviceId = selectedDevice.DeviceId;
        var selectedCamera = new SelectedCameraInfo(selectedDevice.DeviceId, selectedDevice.DisplayName);
        var backendCandidate = selectedSnapshot.BackendName;
        var modeList = (await selectedSnapshot.ModeProvider.GetSupportedModesAsync(selectedDeviceId, cancellationToken)).ToList();

        if (modeList.Count == 0)
        {
            blockingIssues.Add("No supported capture modes were discovered for the selected camera.");
        }

        var exposureLockCapabilityStatus = GetLockCapabilityStatus(backendCandidate);
        var whiteBalanceLockCapabilityStatus = GetLockCapabilityStatus(backendCandidate);
        var exposureVerificationSupported = IsLockCapabilitySupported(exposureLockCapabilityStatus);
        var whiteBalanceVerificationSupported = IsLockCapabilitySupported(whiteBalanceLockCapabilityStatus);
        var timestampReadinessPass = !string.Equals(backendCandidate, "unknown", StringComparison.OrdinalIgnoreCase);

        if (settings.LockExposure && !exposureVerificationSupported)
        {
            blockingIssues.Add($"Exposure lock verification status is '{exposureLockCapabilityStatus}' for the selected backend.");
        }

        if (settings.LockWhiteBalance && !whiteBalanceVerificationSupported)
        {
            blockingIssues.Add($"White balance lock verification status is '{whiteBalanceLockCapabilityStatus}' for the selected backend.");
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
            ExposureLockCapabilityStatus: exposureLockCapabilityStatus,
            WhiteBalanceLockCapabilityStatus: whiteBalanceLockCapabilityStatus,
            TimestampReadinessPass: timestampReadinessPass,
            BlockingIssues: blockingIssues,
            Warnings: warnings,
            Summary: summary);
    }

    private async Task<CapturePreflightResult> EvaluateUsingInjectedProvidersAsync(
        ScanSession session,
        CaptureSettings settings,
        CancellationToken cancellationToken)
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
                ExposureLockCapabilityStatus: LockVerificationStatus.Unknown,
                WhiteBalanceLockCapabilityStatus: LockVerificationStatus.Unknown,
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

        var exposureLockCapabilityStatus = GetLockCapabilityStatus(backendCandidate);
        var whiteBalanceLockCapabilityStatus = GetLockCapabilityStatus(backendCandidate);
        var exposureVerificationSupported = IsLockCapabilitySupported(exposureLockCapabilityStatus);
        var whiteBalanceVerificationSupported = IsLockCapabilitySupported(whiteBalanceLockCapabilityStatus);
        var timestampReadinessPass = !string.Equals(backendCandidate, "unknown", StringComparison.OrdinalIgnoreCase);

        if (settings.LockExposure && !exposureVerificationSupported)
        {
            blockingIssues.Add($"Exposure lock verification status is '{exposureLockCapabilityStatus}' for the selected backend.");
        }

        if (settings.LockWhiteBalance && !whiteBalanceVerificationSupported)
        {
            blockingIssues.Add($"White balance lock verification status is '{whiteBalanceLockCapabilityStatus}' for the selected backend.");
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
            ExposureLockCapabilityStatus: exposureLockCapabilityStatus,
            WhiteBalanceLockCapabilityStatus: whiteBalanceLockCapabilityStatus,
            TimestampReadinessPass: timestampReadinessPass,
            BlockingIssues: blockingIssues,
            Warnings: warnings,
            Summary: summary);
    }

    private static string GetLockCapabilityStatus(string backendCandidate)
    {
        if (string.Equals(backendCandidate, "windows-dshow", StringComparison.OrdinalIgnoreCase)
            || string.Equals(backendCandidate, "opencv", StringComparison.OrdinalIgnoreCase))
        {
            return LockVerificationStatus.Supported;
        }

        if (string.Equals(backendCandidate, "mock", StringComparison.OrdinalIgnoreCase))
        {
            return LockVerificationStatus.Unsupported;
        }

        return LockVerificationStatus.Unknown;
    }

    private static bool IsLockCapabilitySupported(string status)
    {
        return string.Equals(status, LockVerificationStatus.Supported, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<(string BackendName, ICameraDeviceDiscovery DeviceDiscovery, ICameraModeProvider ModeProvider)> BuildCandidateBackends(CaptureSettings settings)
    {
        var preferred = settings.PreferredBackend?.Trim().ToLowerInvariant();
        var ordered = new List<(string BackendName, ICameraDeviceDiscovery DeviceDiscovery, ICameraModeProvider ModeProvider)>();

        void AddBackend(string backendName)
        {
            if (ordered.Any(item => string.Equals(item.BackendName, backendName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (string.Equals(backendName, "windows-dshow", StringComparison.OrdinalIgnoreCase))
            {
                if (!OperatingSystem.IsWindows())
                {
                    throw new InvalidOperationException("Windows capture backend is unavailable on this platform.");
                }

                ordered.Add(("windows-dshow", new WindowsCameraDeviceDiscovery(), new WindowsCameraModeProvider()));
                return;
            }

            if (string.Equals(backendName, "opencv", StringComparison.OrdinalIgnoreCase))
            {
                ordered.Add(("opencv", new OpenCvCameraDeviceDiscovery(), new OpenCvCameraModeProvider()));
                return;
            }

            if (string.Equals(backendName, "mock", StringComparison.OrdinalIgnoreCase))
            {
                ordered.Add(("mock", new MockCameraDeviceDiscovery(), new MockCameraModeProvider()));
                return;
            }

            throw new InvalidOperationException($"Unsupported backend '{backendName}'.");
        }

        if (!string.IsNullOrWhiteSpace(preferred))
        {
            AddBackend(preferred);
            return ordered;
        }

        if (OperatingSystem.IsWindows())
        {
            AddBackend("windows-dshow");
            AddBackend("opencv");
        }

        if (settings.AllowMockFallback)
        {
            AddBackend("mock");
        }

        return ordered;
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
