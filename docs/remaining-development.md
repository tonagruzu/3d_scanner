# Remaining Development Roadmap

This document tracks the remaining implementation work needed to complete MVP scope and harden the pipeline for reliable real-camera operation.

## Source References (Architecture + Principles)
- Architecture baseline: [architecture.md](architecture.md)
- Product scope and acceptance: [requirements.md](requirements.md)
- Accuracy and quality gates: [accuracy-validation.md](accuracy-validation.md)
- Capture discipline: [capture-protocol.md](capture-protocol.md)
- Engineering principles and quality gates: [../AGENTS.md](../AGENTS.md)

## Guiding Constraints
- Keep pipeline stages deterministic and artifact-producing.
- Preserve explicit millimeter semantics and coordinate assumptions.
- Maintain traceable run metadata across capture, calibration, and validation.
- Enforce hard quality gates before release (underlay + dimensional tolerance).

## Current Status (2026-02-23)

### Implemented Recently
- Real camera backend paths are active (`windows-dshow`, `opencv`) with explicit backend routing and policy-controlled mock fallback.
- Capture preflight fail-fast gate is implemented and persisted in `validation.json` (`capturePreflight`).
- Capture capability metadata is persisted (`selectedCamera`, `modeList`, `backendUsed`).
- Capture diagnostics include lock verification signals, timestamp source, monotonic timestamp check, and reliability warnings.
- Pipeline success now includes capture acceptance gating (runs with `0` accepted frames no longer report `PASS`).
- GUI now includes:
	- camera picker + refresh,
	- preflight summary,
	- post-capture frame preview,
	- live preview updates,
	- manual preview refresh,
	- frame-quality metrics with required pass thresholds.

### Phase Progress Snapshot
- Phase 1: **in progress** (major milestones complete; reliability-in-session still needs hardening)
- Phase 2: not started
- Phase 3: not started
- Phase 4: not started
- Phase 5: not started
- Phase 6: not started

### Main Remaining Gaps in Phase 1
- Add robust in-session reliability controller (minimum accepted target + bounded recapture attempts).
- Upgrade timestamping from system-clock approximation to backend-originated frame timing where available.
- Improve lock verification depth (provider capability probe + explicit unsupported/error states by backend/device).

## Remaining Plan (Execution Order)

### Phase 1: Real Camera Integration
- Replace mock device/frame providers with Windows camera backend (Media Foundation/DirectShow wrapper).
- Add camera capability negotiation, exposure/WB lock verification, and frame timestamping.
- Ensure capture metadata remains traceable per session (device, backend, selected mode).
- Done when live USB camera frames are acquired reliably in-session.

### Phase 2: Real Calibration + Underlay Detection
- Implement intrinsic/extrinsic calibration and distortion correction.
- Detect and fit the 10 mm grid (+ optional fiducials), then compute scale confidence.
- Persist calibration residual samples and underlay verification metrics as first-class artifacts.
- Done when calibration residual and underlay scale checks are computed from real images.

### Phase 3: Reconstruction Core
- Replace placeholder geometry with real sparse/dense reconstruction pipeline stages.
- Add meshing from reconstructed points (not synthetic box) while preserving `mm` units.
- Persist intermediate reconstruction artifacts for deterministic debugging and regression.
- Done when generated mesh reflects captured object geometry.

### Phase 4: True Dimensioning + 6 Sketches
- Compute dimensions from reconstructed geometry (not profile stubs).
- Generate orthographic sketches from model orientation and extracted edges.
- Preserve explicit coordinate-system and unit assumptions in exported artifacts.
- Done when sketches/dimensions are derived from actual scan output.

### Phase 5: Accuracy Gates (±0.5 mm)
- Build a metrology dataset and automated regression campaign.
- Enforce hard fail in CI if max absolute error > 0.5 mm for in-scope objects.
- Track mean error and repeatability trends as secondary stability indicators.
- Done when repeated benchmark runs pass tolerance criteria.

### Phase 6: Release Hardening
- Finalize packaging/installer, logs/diagnostics, and operator workflow polish.
- Pilot on representative parts and close reliability issues from pilot feedback.
- Ensure release checklist includes CI/security pass + metrology impact notes.
- Done when pilot acceptance criteria pass.

## Cross-Cutting Work
- Add integration tests for end-to-end pipeline invariants (non-hardware and smoke-hardware paths).
- Extend docs for operational troubleshooting and expected artifact schema evolution.
- Add structured risk/rollback notes for accuracy-sensitive changes.

## Definition of Done for MVP Completion
- End-to-end pipeline produces non-placeholder mesh and six sketches.
- Validation report demonstrates max absolute error <= 0.5 mm on in-scope artifacts.
- CI includes enforced gates aligned with accuracy plan and security checks.
- Documentation covers operator workflow, artifact contracts, and troubleshooting.
