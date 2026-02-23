# 3D Scanner

Windows desktop application for 3D object scanning using an external USB HD camera, with CAD-consumable outputs and dimensioned orthographic sketches.

## Vision
- Capture small rigid objects (target range: 20-200 mm) in controlled lighting.
- Reconstruct a 3D model suitable for CAD workflows (Autodesk Fusion compatible).
- Generate six dimensioned orthographic 2D sketches (Front, Back, Left, Right, Top, Bottom).
- Achieve strict absolute dimensional precision target: ±0.5 mm for in-scope objects.

## Current Status
Repository bootstrap in progress.

## Planned Architecture (high level)
- `src/App/Capture`: Camera control and guided acquisition.
- `src/App/Calibration`: Intrinsic/extrinsic calibration and scale enforcement.
- `src/App/Reconstruction`: Multi-view reconstruction pipeline.
- `src/App/Meshing`: Mesh generation and cleanup.
- `src/App/Dimensioning`: Orthographic projections and dimensions.
- `src/App/Export`: OBJ/STL and sketch export.
- `src/App/Validation`: Accuracy and repeatability gates.

## Development Model
This project is designed for AI-agent-assisted development using GitHub Copilot and GitHub workflows.

## Getting Started
Detailed setup and contribution instructions will be expanded as implementation progresses. See:
- `CONTRIBUTING.md`
- `AGENTS.md`
- `docs/requirements.md`
