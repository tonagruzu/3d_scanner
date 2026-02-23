using Scanner3D.Core.Models;
using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class CaptureServiceTests
{
    [Fact]
    public async Task CaptureAsync_UsesRequestedCameraWhenAvailable()
    {
        var service = new CaptureService(new MockCameraDeviceDiscovery(), new MockFrameCaptureProvider());
        var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "usb-hd-cam-01", "capture-test");
        var settings = new CaptureSettings(8, true, true, "Mata-10mm-grid", "diffuse");

        var result = await service.CaptureAsync(session, settings);

        Assert.Equal("usb-hd-cam-01", result.CameraDeviceId);
        Assert.Equal(8, result.CapturedFrameCount);
        Assert.True(result.AcceptedFrameCount > 0);
        Assert.Contains("underlay=Mata-10mm-grid", result.Notes);
    }

    [Fact]
    public async Task CaptureAsync_FallsBackToAvailableCameraWhenRequestedNotFound()
    {
        var service = new CaptureService(new MockCameraDeviceDiscovery(), new MockFrameCaptureProvider());
        var session = new ScanSession(Guid.NewGuid(), DateTimeOffset.UtcNow, "missing-camera", "capture-test");
        var settings = new CaptureSettings(5, true, false, "Mata-10mm-grid", "diffuse");

        var result = await service.CaptureAsync(session, settings);

        Assert.NotEqual("missing-camera", result.CameraDeviceId);
        Assert.Equal(5, result.CapturedFrameCount);
        Assert.True(result.AcceptedFrameCount >= 1);
    }
}
