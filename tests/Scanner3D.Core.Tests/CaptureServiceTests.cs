using Scanner3D.Core.Models;
using Scanner3D.Core.Services;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class CaptureServiceTests
{
    [Fact]
    public async Task CaptureAsync_UsesRequestedCameraWhenAvailable()
    {
        var service = new CaptureService(new MockCameraDeviceDiscovery(), new MockCameraModeProvider(), new MockFrameCaptureProvider());
        var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "usb-hd-cam-01", "capture-test");
        var settings = new CaptureSettings(8, true, true, "Mata-10mm-grid", "diffuse");

        var result = await service.CaptureAsync(session, settings);

        Assert.Equal("usb-hd-cam-01", result.CameraDeviceId);
        Assert.Equal(8, result.CapturedFrameCount);
        Assert.True(result.AcceptedFrameCount > 0);
        Assert.Equal(1920, result.SelectedMode.Width);
        Assert.Equal(1080, result.SelectedMode.Height);
        Assert.Equal("mock", result.CaptureBackend);
        Assert.True(result.ExposureLockRequested);
        Assert.True(result.WhiteBalanceLockRequested);
        Assert.Null(result.ExposureLockVerified);
        Assert.Null(result.WhiteBalanceLockVerified);
        Assert.Equal("system_clock_utc", result.FrameTimestampSource);
        Assert.True(result.FrameTimestampsMonotonic);
        Assert.Contains("mode=", result.Notes);
        Assert.Contains("underlay=Mata-10mm-grid", result.Notes);
    }

    [Fact]
    public async Task CaptureAsync_FallsBackToAvailableCameraWhenRequestedNotFound()
    {
        var service = new CaptureService(new MockCameraDeviceDiscovery(), new MockCameraModeProvider(), new MockFrameCaptureProvider());
        var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "missing-camera", "capture-test");
        var settings = new CaptureSettings(5, true, false, "Mata-10mm-grid", "diffuse");

        var result = await service.CaptureAsync(session, settings);

        Assert.NotEqual("missing-camera", result.CameraDeviceId);
        Assert.Equal(5, result.CapturedFrameCount);
        Assert.True(result.AcceptedFrameCount >= 1);
    }

    [Fact]
    public async Task CaptureAsync_UsesDevicePreferredModeOverModeProviderResult()
    {
        var preferredMode = new CameraCaptureMode(1024, 768, 24, "RGB24");
        var modeProvider = new RecordingModeProvider(
        [
            new CameraCaptureMode(1920, 1080, 30, "MJPG"),
            new CameraCaptureMode(1280, 720, 30, "YUY2")
        ]);
        var frameProvider = new RecordingFrameProvider(
        [
            new CaptureFrame("f-001", DateTimeOffset.UtcNow, 0.9, 0.9, true)
        ]);

        var service = new CaptureService(
            new StaticDeviceDiscovery([
                new CameraDeviceInfo("cam-a", "Preferred Cam", true, preferredMode)
            ]),
            modeProvider,
            frameProvider);

        var result = await service.CaptureAsync(
            new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "cam-a", "provider-contract"),
            new CaptureSettings(3, true, true, "Mata-10mm-grid", "diffuse"));

        Assert.Equal(preferredMode, result.SelectedMode);
        Assert.Equal("cam-a", modeProvider.LastCameraDeviceId);
        Assert.Equal("cam-a", frameProvider.LastCameraDeviceId);
    }

    [Fact]
    public async Task CaptureAsync_UsesModeProviderWhenPreferredModeIsUnavailable()
    {
        var selectedFromProvider = new CameraCaptureMode(800, 600, 20, "GRAY8");
        var modeProvider = new RecordingModeProvider([selectedFromProvider]);

        var service = new CaptureService(
            new StaticDeviceDiscovery([
                new CameraDeviceInfo("cam-b", "No Preferred Cam", true, null)
            ]),
            modeProvider,
            new RecordingFrameProvider([
                new CaptureFrame("f-001", DateTimeOffset.UtcNow, 0.9, 0.9, true)
            ]));

        var result = await service.CaptureAsync(
            new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "cam-b", "provider-contract"),
            new CaptureSettings(3, true, true, "Mata-10mm-grid", "diffuse"));

        Assert.Equal(selectedFromProvider, result.SelectedMode);
        Assert.Equal("cam-b", modeProvider.LastCameraDeviceId);
    }

    [Fact]
    public async Task CaptureAsync_UsesSessionCameraIdWhenNoAvailableDeviceIsDiscovered()
    {
        var modeProvider = new RecordingModeProvider([new CameraCaptureMode(640, 480, 30, "Unknown")]);
        var frameProvider = new RecordingFrameProvider([
            new CaptureFrame("session-cam-f-001", DateTimeOffset.UtcNow, 0.88, 0.86, true)
        ]);
        var sessionCameraId = "session-cam-id";

        var service = new CaptureService(
            new StaticDeviceDiscovery([
                new CameraDeviceInfo("offline-cam", "Offline Cam", false, null)
            ]),
            modeProvider,
            frameProvider);

        var result = await service.CaptureAsync(
            new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, sessionCameraId, "provider-contract"),
            new CaptureSettings(3, false, false, "Mata-10mm-grid", "diffuse"));

        Assert.Equal(sessionCameraId, result.CameraDeviceId);
        Assert.Equal(sessionCameraId, modeProvider.LastCameraDeviceId);
        Assert.Equal(sessionCameraId, frameProvider.LastCameraDeviceId);
    }

    private sealed class StaticDeviceDiscovery : ICameraDeviceDiscovery
    {
        private readonly IReadOnlyList<CameraDeviceInfo> _devices;

        public StaticDeviceDiscovery(IReadOnlyList<CameraDeviceInfo> devices)
        {
            _devices = devices;
        }

        public Task<IReadOnlyList<CameraDeviceInfo>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_devices);
        }
    }

    private sealed class RecordingModeProvider : ICameraModeProvider
    {
        private readonly IReadOnlyList<CameraCaptureMode> _modes;

        public RecordingModeProvider(IReadOnlyList<CameraCaptureMode> modes)
        {
            _modes = modes;
        }

        public string? LastCameraDeviceId { get; private set; }

        public Task<IReadOnlyList<CameraCaptureMode>> GetSupportedModesAsync(
            string cameraDeviceId,
            CancellationToken cancellationToken = default)
        {
            LastCameraDeviceId = cameraDeviceId;
            return Task.FromResult(_modes);
        }
    }

    private sealed class RecordingFrameProvider : IFrameCaptureProvider
    {
        private readonly IReadOnlyList<CaptureFrame> _frames;

        public RecordingFrameProvider(IReadOnlyList<CaptureFrame> frames)
        {
            _frames = frames;
        }

        public string? LastCameraDeviceId { get; private set; }

        public Task<FrameCaptureResult> CaptureFramesAsync(
            string cameraDeviceId,
            CaptureSettings settings,
            CancellationToken cancellationToken = default)
        {
            LastCameraDeviceId = cameraDeviceId;
            return Task.FromResult(new FrameCaptureResult(
                Frames: _frames,
                Diagnostics: new FrameCaptureDiagnostics(
                    BackendUsed: "test-double",
                    ExposureLockVerified: true,
                    WhiteBalanceLockVerified: true,
                    TimestampSource: "system_clock_utc")));
        }
    }
}
