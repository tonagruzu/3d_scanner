# Real-Camera Run Recipe (Field)

Use this recipe to maximize calibration and underlay gate pass-rate on real camera runs.

## Goal
- Keep intrinsic detection from falling into full rejection (`intrinsicFramesUsed = 0`).
- Keep underlay confidence above gate thresholds:
  - `underlayScaleConfidence >= 0.7`
  - `underlayPoseQuality >= 0.45`

## Pre-Run Setup (2-3 minutes)
1. Fix camera on stable mount (no handheld movement).
2. Set diffuse, even light from at least two directions.
3. Remove strong highlights/reflections from board and object.
4. Place checkerboard/grid fully inside frame with margin on all sides.
5. Confirm board fills roughly 25-60% of frame area.
6. Verify focus manually (sharp board corners at center and near edges).

## Capture Framing Rules
- Never crop board edges in the frame.
- Keep at least one sequence with near-frontal board pose.
- Add moderate tilt views (left/right/up/down), avoid extreme perspective.
- Keep shutter blur low (pause motion before each capture).

## Recommended Sequence (single run)
1. Start with 3 near-frontal frames of board + object.
2. Capture 4-6 orbit frames with overlap.
3. Capture 2 top-biased and 2 side-biased frames.
4. Ensure at least 3 frames have clearly visible board corners.

## Quick Operator Checks During Run
- If preview looks soft: stop and refocus before continuing.
- If board appears too small: move camera closer (do not zoom digitally).
- If board is clipped: reframe to include full pattern.
- If lighting flickers: stabilize light source before rerun.

## Post-Run Acceptance Check
Open `validation.json` and verify:
- `calibrationQuality.intrinsicFramesUsed > 0` (prefer `>= 3` in strict runs).
- `calibrationQuality.intrinsicFramesRejected < intrinsicFramesEvaluated`.
- `underlayVerification.geometryDerived = true` (preferred).
- `underlayVerification.homographyInlierRatio` close to `1.0` (higher is better).
- `underlayVerification.poseReprojectionErrorPx` low and stable across runs.
- `calibrationQuality.underlayScaleConfidence >= 0.7`.
- `calibrationQuality.underlayPoseQuality >= 0.45`.

## If Run Fails with Low Confidence
When you see values like `scaleConfidence=0.25` and `poseQuality=0.2`, usually the board was not detected reliably. Retry with:
1. Better board visibility (bigger in frame, no clipping).
2. Higher corner contrast (less glare, more diffuse light).
3. Shorter motion / steadier capture.
4. More frontal frames at beginning of sequence.

## Strict Intrinsic Mode (Optional)
Use only when you need hard evidence from intrinsic checkerboard frames.
- Enable via environment: `SCANNER3D_REQUIRE_INTRINSIC_FRAMES=1`
- Or operator notes: `require-intrinsic` or `calibration-strict`

For normal field runs, strict mode should stay off unless explicitly required.
