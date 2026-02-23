using Scanner3D.Core.Models;

namespace Scanner3D.Core.Services;

public interface ISketchService
{
    Task<IReadOnlyList<string>> GenerateOrthographicSketchesAsync(
        Guid sessionId,
        IReadOnlyList<DimensionMeasurement> measurements,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
