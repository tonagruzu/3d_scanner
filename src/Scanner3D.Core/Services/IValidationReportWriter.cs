using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface IValidationReportWriter
{
    Task<string> WriteAsync(ScanQualityReport report, string outputDirectory, CancellationToken cancellationToken = default);
}
