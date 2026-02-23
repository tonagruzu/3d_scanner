using OpenCvSharp;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class OpenCvCameraModeProvider : ICameraModeProvider
{
    private const int ProbeLimit = 6;

    public Task<IReadOnlyList<CameraCaptureMode>> GetSupportedModesAsync(
        string cameraDeviceId,
        CancellationToken cancellationToken = default)
    {
        var index = ParseIndex(cameraDeviceId);
        if (index is null)
        {
            return Task.FromResult<IReadOnlyList<CameraCaptureMode>>([]);
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var capture = new VideoCapture(index.Value, VideoCaptureAPIs.DSHOW);
        if (!capture.IsOpened())
        {
            return Task.FromResult<IReadOnlyList<CameraCaptureMode>>([]);
        }

        var width = (int)Math.Max(640, capture.Get(VideoCaptureProperties.FrameWidth));
        var height = (int)Math.Max(480, capture.Get(VideoCaptureProperties.FrameHeight));
        var fps = (int)Math.Max(15, capture.Get(VideoCaptureProperties.Fps));

        IReadOnlyList<CameraCaptureMode> modes =
        [
            new(width, height, fps, "BGR24"),
            new(1280, 720, 30, "BGR24"),
            new(640, 480, 30, "BGR24")
        ];

        return Task.FromResult(modes);
    }

    private static int? ParseIndex(string cameraDeviceId)
    {
        if (cameraDeviceId.StartsWith("opencv-camera-", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(cameraDeviceId["opencv-camera-".Length..], out var prefixedIndex))
        {
            return prefixedIndex;
        }

        if (int.TryParse(cameraDeviceId, out var directIndex) && directIndex >= 0 && directIndex < ProbeLimit)
        {
            return directIndex;
        }

        return null;
    }
}
