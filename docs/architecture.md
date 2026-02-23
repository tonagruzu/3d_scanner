# Architecture Overview

## Modules
- Capture: camera discovery, acquisition, frame quality scoring.
- Calibration: intrinsic/extrinsic solve, distortion correction, scale enforcement.
- Reconstruction: feature matching, alignment, dense reconstruction.
- Meshing: surface generation, cleanup, watertightness checks.
- Dimensioning: canonical orientation, orthographic projection, dimension extraction.
- Export: OBJ/STL mesh and 2D sketch package export.
- Validation: dimensional comparison and pass/fail gates.

## Design Principles
- Deterministic pipeline stages with persisted intermediate artifacts.
- Explicit unit handling (mm) across all stages.
- Traceable metadata for each scan session and result.
- Strict quality gates before release.

## Near-term Implementation Strategy
1. Build module contracts and session model.
2. Implement stub pipeline with deterministic placeholders.
3. Replace stubs incrementally with functional algorithms.
4. Add validation harness and enforce CI gate.
