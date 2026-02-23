namespace Scanner3D.Core.Models;

public sealed record CalibrationResult(
    string CalibrationProfileId,
    DateTimeOffset CalibratedAt,
    double ReprojectionErrorPx,
    double ScaleErrorMm,
    bool IsWithinTolerance,
    string Notes,
    IntrinsicCalibrationDetails? IntrinsicCalibration = null);
