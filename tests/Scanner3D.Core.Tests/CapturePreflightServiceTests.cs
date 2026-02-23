using Scanner3D.Core.Models;
using Scanner3D.Core.Services;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class CapturePreflightServiceTests
{
    [Fact]
    public async Task EvaluateAsync_Fails_WhenNoAvailableCamera()
    {
        var service = new CapturePreflightService(
            new StaticDeviceDiscovery([]),
            new StaticModeProvider([]));

        var result = await service.EvaluateAsync(
            new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "missing-device", "test"),
            new CaptureSettings(3, true, true, "grid", "diffuse", AllowMockFallback: false));

        Assert.False(result.Pass);
        Assert.Contains(result.BlockingIssues, issue => issue.Contains("No available camera", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_Fails_WhenMockFallbackIsDisallowed()
    {
        var service = new CapturePreflightService(
            new StaticDeviceDiscovery([
                new CameraDeviceInfo("bootstrap-device", "Bootstrap USB Camera", true, null)
            ]),
            new StaticModeProvider([
                new CameraCaptureMode(1920, 1080, 30, "MJPG")
            ]));

        var result = await service.EvaluateAsync(
            new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "bootstrap-device", "prod-run"),
            new CaptureSettings(3, true, true, "grid", "diffuse", AllowMockFallback: false));

        Assert.False(result.Pass);
        Assert.Contains(result.BlockingIssues, issue => issue.Contains("Mock fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_Passes_WhenMockFallbackAllowedForTestMode()
    {
        var service = new CapturePreflightService(
            new StaticDeviceDiscovery([
                new CameraDeviceInfo("bootstrap-device", "Bootstrap USB Camera", true, null)
            ]),
            new StaticModeProvider([
                new CameraCaptureMode(1920, 1080, 30, "MJPG")
            ]));

        var result = await service.EvaluateAsync(
            new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "bootstrap-device", "test-run"),
            new CaptureSettings(3, false, false, "grid", "diffuse", AllowMockFallback: true));

        Assert.True(result.Pass);
        Assert.Equal("mock", result.BackendCandidate);
        Assert.Single(result.ModeList);
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

    private sealed class StaticModeProvider : ICameraModeProvider
    {
        private readonly IReadOnlyList<CameraCaptureMode> _modes;

        public StaticModeProvider(IReadOnlyList<CameraCaptureMode> modes)
        {
            _modes = modes;
        }

        public Task<IReadOnlyList<CameraCaptureMode>> GetSupportedModesAsync(string cameraDeviceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_modes);
        }
    }
}
