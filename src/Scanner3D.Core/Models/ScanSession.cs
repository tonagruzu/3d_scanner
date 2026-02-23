namespace Scanner3D.Core.Models;

public sealed record ScanSession(
    Guid SessionId,
    DateTimeOffset StartedAt,
    string CameraDeviceId,
    string OperatorNotes,
    string Units = "mm");
