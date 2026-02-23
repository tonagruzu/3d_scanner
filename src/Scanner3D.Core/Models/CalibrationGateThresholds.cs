namespace Scanner3D.Core.Models;

public static class CalibrationGateThresholds
{
    public const int MinUsableIntrinsicFrames = 3;
    public const double MaxReprojectionErrorPx = 0.5;
    public const double MinUnderlayScaleConfidence = 0.7;
    public const double MinUnderlayPoseQuality = 0.45;
}
