using System.Text.Json;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class JsonValidationReportWriter : IValidationReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<string> WriteAsync(ScanQualityReport report, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "validation.json");
        var reportWithCapabilities = report with { CaptureCapabilities = report.CaptureCapabilities ?? BuildCaptureCapabilities(report) };

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, reportWithCapabilities, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        return outputPath;
    }

    private static CaptureCapabilityDetails BuildCaptureCapabilities(ScanQualityReport report)
    {
        var displayName = TryExtractCaptureNoteValue(report.Capture.Notes, "device")
            ?? report.Capture.CameraDeviceId;

        var backend = string.IsNullOrWhiteSpace(report.Capture.CaptureBackend)
            ? InferBackend(report.Capture.CameraDeviceId, report.Capture.Frames)
            : report.Capture.CaptureBackend;
        var supportedModes = GetSupportedModes(backend, report.Capture.SelectedMode);

        return new CaptureCapabilityDetails(
            SelectedCamera: new SelectedCameraInfo(report.Capture.CameraDeviceId, displayName),
            ModeList: supportedModes,
            BackendUsed: backend);
    }

    private static string InferBackend(string cameraDeviceId, IReadOnlyList<CaptureFrame> frames)
    {
        if (frames.Any(frame => frame.FrameId.StartsWith("win-cam-", StringComparison.OrdinalIgnoreCase)))
        {
            return "windows";
        }

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
            return "windows";
        }

        return "unknown";
    }

    private static IReadOnlyList<CameraCaptureMode> GetSupportedModes(string backend, CameraCaptureMode selectedMode)
    {
        var modes = new List<CameraCaptureMode> { selectedMode };

        if (string.Equals(backend, "windows", StringComparison.OrdinalIgnoreCase))
        {
            modes.Add(new CameraCaptureMode(1920, 1080, 30, "Unknown"));
            modes.Add(new CameraCaptureMode(1280, 720, 30, "Unknown"));
        }
        else if (string.Equals(backend, "opencv", StringComparison.OrdinalIgnoreCase))
        {
            modes.Add(new CameraCaptureMode(1280, 720, 30, "BGR24"));
            modes.Add(new CameraCaptureMode(640, 480, 30, "BGR24"));
        }
        else if (string.Equals(backend, "mock", StringComparison.OrdinalIgnoreCase))
        {
            modes.Add(new CameraCaptureMode(1920, 1080, 30, "MJPG"));
            modes.Add(new CameraCaptureMode(1280, 720, 60, "YUY2"));
            modes.Add(new CameraCaptureMode(1280, 720, 30, "YUY2"));
        }

        return modes.Distinct().ToList();
    }

    private static string? TryExtractCaptureNoteValue(string notes, string key)
    {
        if (string.IsNullOrWhiteSpace(notes) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var segments = notes.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            var keyValue = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (keyValue.Length != 2)
            {
                continue;
            }

            if (string.Equals(keyValue[0], key, StringComparison.OrdinalIgnoreCase))
            {
                return keyValue[1];
            }
        }

        return null;
    }
}
