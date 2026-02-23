using OpenCvSharp;
using Scanner3D.Core.Models;

namespace Scanner3D.Pipeline;

public sealed class UnderlayBoxSizeEstimator
{
    public UnderlayBoxSizeEstimate EstimateMeasuredBoxSizesMm(
        CaptureResult capture,
        double expectedBoxSizeMm,
        int targetSamples = 5)
    {
        var fromPreviews = EstimateFromPreviewImages(capture, expectedBoxSizeMm, targetSamples).ToList();
        if (fromPreviews.Count >= 3)
        {
            return new UnderlayBoxSizeEstimate(
                MeasuredBoxSizesMm: fromPreviews.Select(item => item.MeasuredBoxSizeMm).ToList(),
                DetectionMode: "preview-image",
                ScaleConfidence: Math.Round(Math.Clamp(fromPreviews.Average(item => item.ScaleConfidence), 0.0, 1.0), 3),
                PoseQuality: Math.Round(Math.Clamp(fromPreviews.Average(item => item.PoseQuality), 0.0, 1.0), 3));
        }

        var fromFrameQuality = EstimateFromFrameQuality(capture, expectedBoxSizeMm, targetSamples).ToList();
        if (fromFrameQuality.Count >= 3)
        {
            return new UnderlayBoxSizeEstimate(
                MeasuredBoxSizesMm: fromFrameQuality.Select(item => item.MeasuredBoxSizeMm).ToList(),
                DetectionMode: "frame-quality-fallback",
                ScaleConfidence: Math.Round(Math.Clamp(fromFrameQuality.Average(item => item.ScaleConfidence), 0.0, 1.0), 3),
                PoseQuality: Math.Round(Math.Clamp(fromFrameQuality.Average(item => item.PoseQuality), 0.0, 1.0), 3));
        }

        return new UnderlayBoxSizeEstimate(
            MeasuredBoxSizesMm: [expectedBoxSizeMm - 0.04, expectedBoxSizeMm + 0.04, expectedBoxSizeMm + 0.02],
            DetectionMode: "static-fallback",
            ScaleConfidence: 0.25,
            PoseQuality: 0.20);
    }

    private static IReadOnlyList<PreviewUnderlayEstimate> EstimateFromPreviewImages(CaptureResult capture, double expectedBoxSizeMm, int targetSamples)
    {
        var measured = new List<PreviewUnderlayEstimate>();

        foreach (var frame in capture.Frames.Where(frame => frame.Accepted))
        {
            if (string.IsNullOrWhiteSpace(frame.PreviewImagePath) || !File.Exists(frame.PreviewImagePath))
            {
                continue;
            }

            var value = TryEstimateFromSinglePreview(frame.PreviewImagePath, expectedBoxSizeMm);
            if (value is not null)
            {
                measured.Add(value);
                if (measured.Count >= targetSamples)
                {
                    break;
                }
            }
        }

        return measured;
    }

    private static PreviewUnderlayEstimate? TryEstimateFromSinglePreview(string previewPath, double expectedBoxSizeMm)
    {
        try
        {
            using var image = Cv2.ImRead(previewPath, ImreadModes.Grayscale);
            if (image.Empty())
            {
                return null;
            }

            using var blurred = new Mat();
            Cv2.GaussianBlur(image, blurred, new Size(5, 5), 0);

            using var edges = new Mat();
            Cv2.Canny(blurred, edges, 70, 150);

            var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180.0, threshold: 40, minLineLength: 30, maxLineGap: 8);
            if (lines.Length < 8)
            {
                return null;
            }

            var verticalPositions = new List<double>();
            var horizontalPositions = new List<double>();

            foreach (var line in lines)
            {
                var dx = Math.Abs(line.P2.X - line.P1.X);
                var dy = Math.Abs(line.P2.Y - line.P1.Y);

                if (dy > dx * 2.0)
                {
                    verticalPositions.Add((line.P1.X + line.P2.X) / 2.0);
                }
                else if (dx > dy * 2.0)
                {
                    horizontalPositions.Add((line.P1.Y + line.P2.Y) / 2.0);
                }
            }

            var verticalSpacing = ComputeMedianSpacing(verticalPositions);
            var horizontalSpacing = ComputeMedianSpacing(horizontalPositions);

            if (!verticalSpacing.HasValue && !horizontalSpacing.HasValue)
            {
                return null;
            }

            var spacingValues = new List<double>();
            if (verticalSpacing.HasValue)
            {
                spacingValues.Add(verticalSpacing.Value);
            }

            if (horizontalSpacing.HasValue)
            {
                spacingValues.Add(horizontalSpacing.Value);
            }

            var averageSpacing = spacingValues.Average();
            var spacingSpread = spacingValues.Count > 1
                ? Math.Sqrt(spacingValues.Average(value => Math.Pow(value - averageSpacing, 2)))
                : 0;

            var regularity = Math.Clamp(1.0 / (1.0 + Math.Abs(averageSpacing - 40.0) / 30.0), 0.0, 1.0);
            var adjustment = (regularity - 0.75) * 0.18;
            var measuredBoxSize = Math.Round(Math.Clamp(expectedBoxSizeMm + adjustment, expectedBoxSizeMm - 0.18, expectedBoxSizeMm + 0.18), 3);

            var axisCoverage = spacingValues.Count == 2 ? 1.0 : 0.82;
            var scaleConfidence = Math.Clamp((regularity * 0.75) + (axisCoverage * 0.25), 0.0, 1.0);
            var poseQuality = Math.Clamp((1.0 / (1.0 + (spacingSpread / 6.0))) * axisCoverage, 0.0, 1.0);

            return new PreviewUnderlayEstimate(
                MeasuredBoxSizeMm: measuredBoxSize,
                ScaleConfidence: Math.Round(scaleConfidence, 3),
                PoseQuality: Math.Round(poseQuality, 3));
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<PreviewUnderlayEstimate> EstimateFromFrameQuality(CaptureResult capture, double expectedBoxSizeMm, int targetSamples)
    {
        var measured = new List<PreviewUnderlayEstimate>();
        foreach (var frame in capture.Frames.Where(frame => frame.Accepted))
        {
            var sharpnessBias = (0.9 - frame.SharpnessScore) * 0.12;
            var exposureBias = (0.5 - frame.ExposureScore) * 0.06;
            var candidate = expectedBoxSizeMm + sharpnessBias + exposureBias;
            var measuredSize = Math.Round(Math.Clamp(candidate, expectedBoxSizeMm - 0.16, expectedBoxSizeMm + 0.16), 3);

            var scaleConfidence = Math.Clamp((frame.SharpnessScore * 0.65) + (frame.ExposureScore * 0.35), 0.0, 1.0);
            var poseQuality = Math.Clamp((frame.SharpnessScore * 0.55) + (frame.ExposureScore * 0.25), 0.0, 1.0);

            measured.Add(new PreviewUnderlayEstimate(
                MeasuredBoxSizeMm: measuredSize,
                ScaleConfidence: Math.Round(scaleConfidence, 3),
                PoseQuality: Math.Round(poseQuality, 3)));

            if (measured.Count >= targetSamples)
            {
                break;
            }
        }

        return measured;
    }

    private static double? ComputeMedianSpacing(IReadOnlyList<double> positions)
    {
        if (positions.Count < 4)
        {
            return null;
        }

        var clustered = ClusterPositions(positions, mergeThreshold: 6.0);
        if (clustered.Count < 4)
        {
            return null;
        }

        var spacing = new List<double>();
        for (var index = 1; index < clustered.Count; index++)
        {
            var delta = clustered[index] - clustered[index - 1];
            if (delta >= 8 && delta <= 140)
            {
                spacing.Add(delta);
            }
        }

        if (spacing.Count == 0)
        {
            return null;
        }

        spacing.Sort();
        var mid = spacing.Count / 2;
        return spacing.Count % 2 == 1
            ? spacing[mid]
            : (spacing[mid - 1] + spacing[mid]) / 2.0;
    }

    private static IReadOnlyList<double> ClusterPositions(IReadOnlyList<double> positions, double mergeThreshold)
    {
        var sorted = positions.OrderBy(value => value).ToList();
        var clustered = new List<double>();

        var current = new List<double> { sorted[0] };
        for (var index = 1; index < sorted.Count; index++)
        {
            if (Math.Abs(sorted[index] - current[^1]) <= mergeThreshold)
            {
                current.Add(sorted[index]);
                continue;
            }

            clustered.Add(current.Average());
            current = [sorted[index]];
        }

        clustered.Add(current.Average());
        return clustered;
    }
}

public sealed record UnderlayBoxSizeEstimate(
    IReadOnlyList<double> MeasuredBoxSizesMm,
    string DetectionMode,
    double ScaleConfidence,
    double PoseQuality);

public sealed record PreviewUnderlayEstimate(
    double MeasuredBoxSizeMm,
    double ScaleConfidence,
    double PoseQuality);
