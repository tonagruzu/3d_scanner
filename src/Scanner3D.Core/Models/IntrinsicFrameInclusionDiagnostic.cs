namespace Scanner3D.Core.Models;

public sealed record IntrinsicFrameInclusionDiagnostic(
    string FrameId,
    bool Included,
    string ReasonCode,
    string ReasonCategory);
