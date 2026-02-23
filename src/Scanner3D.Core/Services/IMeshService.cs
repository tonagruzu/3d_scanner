using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface IMeshService
{
    Task<string> GenerateObjAsync(
        Guid sessionId,
        IReadOnlyList<DimensionMeasurement> measurements,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
