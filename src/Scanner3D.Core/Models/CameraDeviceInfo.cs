namespace Scanner3D.Core.Models;

public sealed record CameraDeviceInfo(
    string DeviceId,
    string DisplayName,
    bool IsAvailable,
    CameraCaptureMode? PreferredMode = null);
