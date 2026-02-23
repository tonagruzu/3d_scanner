using System.Runtime.Versioning;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

[SupportedOSPlatform("windows")]
public sealed class WindowsFrameCaptureProvider : IFrameCaptureProvider
{
    private readonly ICameraDeviceDiscovery _cameraDeviceDiscovery;

    public WindowsFrameCaptureProvider(ICameraDeviceDiscovery? cameraDeviceDiscovery = null)
    {
        _cameraDeviceDiscovery = cameraDeviceDiscovery ?? new WindowsCameraDeviceDiscovery();
    }

    public async Task<IReadOnlyList<CaptureFrame>> CaptureFramesAsync(
        string cameraDeviceId,
        int targetFrameCount,
        CancellationToken cancellationToken = default)
    {
        var devices = await _cameraDeviceDiscovery.GetAvailableDevicesAsync(cancellationToken);
        var isKnownDevice = devices.Any(device =>
            device.IsAvailable && string.Equals(device.DeviceId, cameraDeviceId, StringComparison.OrdinalIgnoreCase));

        if (!isKnownDevice)
        {
            return [];
        }

        var frameCount = Math.Max(3, targetFrameCount);
        var frames = new List<CaptureFrame>(frameCount);

        for (var index = 1; index <= frameCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sharpness = Math.Max(0.7, 0.96 - (index * 0.015));
            var exposure = Math.Max(0.78, 0.94 - ((index % 5) * 0.025));
            var accepted = sharpness >= 0.82 && exposure >= 0.82;

            frames.Add(new CaptureFrame(
                FrameId: $"{cameraDeviceId}-win-f-{index:000}",
                CapturedAt: DateTimeOffset.UtcNow.AddMilliseconds(index * 80),
                SharpnessScore: sharpness,
                ExposureScore: exposure,
                Accepted: accepted));

            await Task.Delay(20, cancellationToken);
        }

        return frames;
    }
}
