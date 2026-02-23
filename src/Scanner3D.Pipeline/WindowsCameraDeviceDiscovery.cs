using System.Management;
using System.Runtime.Versioning;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

[SupportedOSPlatform("windows")]
public sealed class WindowsCameraDeviceDiscovery : ICameraDeviceDiscovery
{
    public Task<IReadOnlyList<CameraDeviceInfo>> GetAvailableDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = new List<CameraDeviceInfo>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT PNPDeviceID, Name FROM Win32_PnPEntity WHERE Name IS NOT NULL");

            using var results = searcher.Get();
            foreach (var result in results.Cast<ManagementBaseObject>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name = result["Name"]?.ToString();
                var pnpDeviceId = result["PNPDeviceID"]?.ToString();

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(pnpDeviceId))
                {
                    continue;
                }

                if (!LooksLikeCamera(name))
                {
                    continue;
                }

                devices.Add(new CameraDeviceInfo(
                    DeviceId: pnpDeviceId,
                    DisplayName: name,
                    IsAvailable: true,
                    PreferredMode: new CameraCaptureMode(1920, 1080, 30, "Unknown")));
            }
        }
        catch
        {
            return Task.FromResult<IReadOnlyList<CameraDeviceInfo>>([]);
        }

        var uniqueDevices = devices
            .GroupBy(device => device.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(device => device.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<CameraDeviceInfo>>(uniqueDevices);
    }

    private static bool LooksLikeCamera(string name)
    {
        return name.Contains("camera", StringComparison.OrdinalIgnoreCase)
               || name.Contains("webcam", StringComparison.OrdinalIgnoreCase)
               || name.Contains("imaging", StringComparison.OrdinalIgnoreCase)
               || name.Contains("uvc", StringComparison.OrdinalIgnoreCase);
    }
}
