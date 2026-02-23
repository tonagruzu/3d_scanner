using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface ICalibrationService
{
    Task<CalibrationResult> CalibrateAsync(ScanSession session, CancellationToken cancellationToken = default);
}
