namespace Scanner3D.Core.Models;

public sealed record IntrinsicDiagnosticsSummary(
    int TotalFramesEvaluated,
    int UsableFrames,
    int RejectedFrames,
    IReadOnlyDictionary<string, int> RejectedFramesByReason,
    IReadOnlyDictionary<string, int> RejectedFramesByCategory,
    IReadOnlyList<IntrinsicFrameInclusionDiagnostic> FrameDiagnostics);
