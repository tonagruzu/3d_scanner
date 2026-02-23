# Product Requirements

## Scope (MVP)
- Platform: Windows desktop application.
- Capture device: external USB HD camera.
- Input object class: rigid, matte, small objects in nominal 20-200 mm size range.
- Environment: well-lit scene with patterned underlay for feature matching and scale stability.
- Workflow: guided multi-pose capture; operator may reposition object to expose hidden surfaces.

## Required Outputs
- CAD-consumable 3D model export for Autodesk Fusion workflows (OBJ/STL, millimeter units).
- Six dimensioned orthographic sketches: Front, Back, Left, Right, Top, Bottom.
- Validation report with dimensional error metrics and run metadata.

## GUI Requirements
- GUI must surface all available pipeline quality and diagnostics information that is persisted in run artifacts (capture, preflight, calibration, underlay, validation), including newly added metrics as they become available.
- GUI must include visible UI versioning in the main window.
- Whenever a new GUI element/metric visualization is added, the GUI version must be incremented in the visible version label.

## Precision Target
- Absolute dimensional error target: ±0.5 mm for in-scope object class.
- Precision target applies to validated key dimensions and acceptance artifacts defined in `docs/accuracy-validation.md`.

## Non-Goals (MVP)
- Transparent/reflective objects.
- Real-time scanning.
- Automatic conversion to parametric CAD history tree.

## Acceptance Summary
- Pipeline completes end-to-end from capture to exports.
- Exports preserve millimeter unit semantics.
- Validation gate blocks releases if max absolute error exceeds 0.5 mm.
- Intrinsic-frame calibration gating is opt-in for normal field runs; strict enforcement is enabled only via `SCANNER3D_REQUIRE_INTRINSIC_FRAMES=1` or operator notes (`require-intrinsic` / `calibration-strict`).
