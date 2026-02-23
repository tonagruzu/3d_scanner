using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface ICaptureService
{
    Task<CaptureResult> CaptureAsync(ScanSession session, CaptureSettings settings, CancellationToken cancellationToken = default);
}
