# Capture Protocol (MVP)

## Scene Preparation
- Use stable platform and diffuse, uniform lighting.
- Place object on high-feature patterned underlay.
- Eliminate shadows and reflections as much as practical.

## Camera Settings
- Lock exposure and white balance for full session.
- Use fixed focal setup and stable mounting.
- Verify focus and sharpness before capture batch.

## Pose Coverage
- Capture full 360° around object with overlap between adjacent frames.
- Reposition object as prompted to expose occluded surfaces.
- Capture additional top/bottom-biased sequences as needed.

## Session Quality Checks
- Reject blurry frames.
- Reject frames with severe over/underexposure.
- Ensure adequate feature coverage on object and underlay.

## Underlay Calibration and Print Verification
- Verify printed grid scale before scanning (single box target: 10 mm x 10 mm).
- Measure multiple boxes from different sheet regions and record measured values.
- Require max absolute print-scale error <= 0.2 mm before accepting scan session.
- Include at least one non-periodic fiducial marker region to reduce grid ambiguity.

## Calibration Discipline
- Perform calibration at defined cadence and after setup disturbance.
- Record calibration artifact IDs and residuals in session metadata.
