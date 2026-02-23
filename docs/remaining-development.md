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
- In-session reliability controller is implemented (minimum accepted target with bounded recapture attempts and explicit failure reason).
- Backend-native frame timestamps are captured where available and persisted timing quality metrics are reported (`timestampCoverageRatio`, `meanInterFrameIntervalMs`, `interFrameIntervalJitterMs`).
- Lock capability probing and explicit lock states are implemented (`supported`/`unsupported`/`error` at preflight and `verified`/`failed`/`unsupported`/`error` at runtime diagnostics).
- Pipeline success now includes capture acceptance gating (runs with `0` accepted frames no longer report `PASS`).
- Calibration now consumes capture context and derives calibration metrics from captured frame quality (with deterministic fallback).
- Underlay verification now uses frame-derived measured box-size estimates from preview images with quality-based fallback.
- Underlay fitting now performs outlier rejection and computes persisted fit confidence (`fitConfidence`) plus persisted detection path (`detectionMode`).
- Calibration quality gates now include deterministic reprojection percentile gating (P95) with actionable failure reasons in artifacts/run summary.
- GUI now includes:
	- camera picker + refresh,
	- preflight summary,
	- post-capture frame preview,
	- live preview updates,
	- manual preview refresh,
	- frame-quality metrics with required pass thresholds.

### Phase Progress Snapshot
- Phase 1: **completed**
- Phase 2: **in progress** (calibration/underlay artifact plumbing active; camera-model and geometric calibration still pending)
- Phase 3: not started
- Phase 4: not started
- Phase 5: not started
- Phase 6: not started

### Top 3 Next Tasks (Most Impactful for Phase 2)
1) **Intrinsic residual diagnostics hardening (frame inclusion quality traceability)**
- Persist and expose per-frame intrinsic inclusion diagnostics (accepted/rejected by detector + reason classes) as first-class calibration quality outputs.
- Add stable summary counters for usable/rejected intrinsic frames by reason category.
- Why high impact: improves operator troubleshooting and gives deterministic evidence for strict calibration runs.

2) **Camera intrinsic calibration from real frames (checkerboard/ChArUco)**
- Implement corner detection and `cv::calibrateCamera`-equivalent flow in pipeline service.
- Persist camera matrix, distortion coefficients, reprojection residual distribution, and frame inclusion/exclusion reasons.
- Why high impact: unlocks physically meaningful geometry and removes placeholder calibration dependency.

3) **Grid pose estimation + mm scale confidence from image geometry**
- Estimate homography/pose from detected 10 mm grid to compute observed spacing in pixel space mapped to `mm`.
- Add explicit `scaleConfidence` and `poseQuality` fields to underlay/calibration artifacts.
- Why high impact: converts underlay checks from heuristic scores to geometric metrology signals.

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
- Keep GUI synchronized with available artifact metrics and diagnostics; whenever new GUI elements/metric views are added, increment the visible GUI version label.

## Definition of Done for MVP Completion
- End-to-end pipeline produces non-placeholder mesh and six sketches.
- Validation report demonstrates max absolute error <= 0.5 mm on in-scope artifacts.
- CI includes enforced gates aligned with accuracy plan and security checks.
- Documentation covers operator workflow, artifact contracts, and troubleshooting.
