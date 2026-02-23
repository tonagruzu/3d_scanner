using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface IPipelineOrchestrator
{
    Task<PipelineResult> ExecuteAsync(ScanSession session, CancellationToken cancellationToken = default);
}
