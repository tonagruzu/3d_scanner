namespace Scanner3D.Core.Models;

public sealed record CaptureFrame(
    string FrameId,
    DateTimeOffset CapturedAt,
    double SharpnessScore,
    double ExposureScore,
    bool Accepted,
    string? PreviewImagePath = null);
