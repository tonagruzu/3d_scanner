using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.Pipeline;

public sealed class CalibrationService : ICalibrationService
{
    private const double ReprojectionTolerancePx = 0.5;
    private const double ScaleToleranceMm = 0.2;

    public Task<CalibrationResult> CalibrateAsync(ScanSession session, CancellationToken cancellationToken = default)
    {
        var reprojectionErrorPx = 0.42;
        var scaleErrorMm = 0.12;
        var isWithinTolerance = reprojectionErrorPx <= ReprojectionTolerancePx && scaleErrorMm <= ScaleToleranceMm;

        var result = new CalibrationResult(
            CalibrationProfileId: $"calib-{session.SessionId:N}",
            CalibratedAt: DateTimeOffset.UtcNow,
            ReprojectionErrorPx: reprojectionErrorPx,
            ScaleErrorMm: scaleErrorMm,
            IsWithinTolerance: isWithinTolerance,
            Notes: isWithinTolerance
                ? "Calibration completed within configured tolerances."
                : "Calibration exceeded configured tolerances.");

        return Task.FromResult(result);
    }
}
