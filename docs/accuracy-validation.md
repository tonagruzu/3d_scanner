# Accuracy Validation Plan

## Objective
Demonstrate and continuously enforce absolute dimensional error not exceeding 0.5 mm for MVP in-scope objects.

## Reference Artifacts
- Calibrated gauge objects with known dimensions and traceable measurements.
- At least 3 geometry classes (prismatic, cylindrical, mixed contour).

## Test Protocol
- Minimum repeated scans per artifact: 10.
- Minimum total scans in regression campaign: 30.
- Each scan records:
  - Underlay print verification metrics (expected box size vs measured values)
  - Calibration residuals
  - Capture conditions
  - Reconstructed dimensions
  - Absolute error against reference dimensions

## Metrics
- Underlay max absolute box error (10 mm target): <= 0.2 mm (session gate).
- Max absolute error: must be <= 0.5 mm (hard gate).
- Mean absolute error: tracked trend metric.
- Repeatability (std dev): tracked stability metric.

## CI/Release Gating
- Validation job produces machine-readable report (JSON + summary markdown).
- If underlay print verification exceeds 0.2 mm max absolute error, workflow fails.
- If any required dimension exceeds 0.5 mm absolute error, workflow fails.
- Intrinsic-frame calibration gate is opt-in for standard field runs; enable strict mode only with `SCANNER3D_REQUIRE_INTRINSIC_FRAMES=1` or operator notes (`require-intrinsic` / `calibration-strict`).

## Change Control
Any change to calibration, reconstruction, meshing, or dimensioning code requires rerun of regression subset before merge.
