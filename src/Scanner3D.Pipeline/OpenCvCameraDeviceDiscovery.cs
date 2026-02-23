using OpenCvSharp;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class OpenCvCameraDeviceDiscovery : ICameraDeviceDiscovery
{
    private const int ProbeLimit = 6;

    public Task<IReadOnlyList<CameraDeviceInfo>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = new List<CameraDeviceInfo>();

        for (var index = 0; index < ProbeLimit; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
            if (!capture.IsOpened())
            {
                continue;
            }

            var width = (int)Math.Max(640, capture.Get(VideoCaptureProperties.FrameWidth));
            var height = (int)Math.Max(480, capture.Get(VideoCaptureProperties.FrameHeight));
            var fps = (int)Math.Max(15, capture.Get(VideoCaptureProperties.Fps));

            devices.Add(new CameraDeviceInfo(
                DeviceId: $"opencv-camera-{index}",
                DisplayName: $"OpenCV Camera #{index}",
                IsAvailable: true,
                PreferredMode: new CameraCaptureMode(width, height, fps, "BGR24")));
        }

        return Task.FromResult<IReadOnlyList<CameraDeviceInfo>>(devices);
    }
}
