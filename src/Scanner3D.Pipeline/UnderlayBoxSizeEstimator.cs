using OpenCvSharp;
using Scanner3D.Core.Models;

namespace Scanner3D.Pipeline;

public sealed class UnderlayBoxSizeEstimator
{
    public UnderlayBoxSizeEstimate EstimateMeasuredBoxSizesMm(
        CaptureResult capture,
        double expectedBoxSizeMm,
        IntrinsicCalibrationDetails? intrinsicCalibration = null,
        int targetSamples = 5)
    {
        var fromPreviews = EstimateFromPreviewImages(capture, expectedBoxSizeMm, intrinsicCalibration, targetSamples).ToList();
        if (fromPreviews.Count >= 3)
        {
            return new UnderlayBoxSizeEstimate(
                MeasuredBoxSizesMm: fromPreviews.Select(item => item.MeasuredBoxSizeMm).ToList(),
                DetectionMode: "preview-image",
                ScaleConfidence: Math.Round(Math.Clamp(fromPreviews.Average(item => item.ScaleConfidence), 0.0, 1.0), 3),
                PoseQuality: Math.Round(Math.Clamp(fromPreviews.Average(item => item.PoseQuality), 0.0, 1.0), 3),
                GridSpacingPx: Math.Round(AverageOrZero(fromPreviews.Select(item => item.GridSpacingPx)), 3),
                GridSpacingStdDevPx: Math.Round(AverageOrZero(fromPreviews.Select(item => item.GridSpacingStdDevPx)), 3),
                HomographyInlierRatio: Math.Round(Math.Clamp(AverageOrZero(fromPreviews.Select(item => item.HomographyInlierRatio)), 0.0, 1.0), 3),
                PoseReprojectionErrorPx: Math.Round(AverageOrZero(fromPreviews.Select(item => item.PoseReprojectionErrorPx)), 3),
                GeometryDerived: fromPreviews.All(item => item.GeometryDerived));
        }

        var fromFrameQuality = EstimateFromFrameQuality(capture, expectedBoxSizeMm, targetSamples).ToList();
        if (fromFrameQuality.Count >= 3)
        {
            return new UnderlayBoxSizeEstimate(
                MeasuredBoxSizesMm: fromFrameQuality.Select(item => item.MeasuredBoxSizeMm).ToList(),
                DetectionMode: "frame-quality-fallback",
                ScaleConfidence: Math.Round(Math.Clamp(fromFrameQuality.Average(item => item.ScaleConfidence), 0.0, 1.0), 3),
                PoseQuality: Math.Round(Math.Clamp(fromFrameQuality.Average(item => item.PoseQuality), 0.0, 1.0), 3),
                GridSpacingPx: 0,
                GridSpacingStdDevPx: 0,
                HomographyInlierRatio: 0,
                PoseReprojectionErrorPx: 0,
                GeometryDerived: false);
        }

        return new UnderlayBoxSizeEstimate(
            MeasuredBoxSizesMm: [expectedBoxSizeMm - 0.04, expectedBoxSizeMm + 0.04, expectedBoxSizeMm + 0.02],
            DetectionMode: "static-fallback",
            ScaleConfidence: 0.25,
            PoseQuality: 0.20,
            GridSpacingPx: 0,
            GridSpacingStdDevPx: 0,
            HomographyInlierRatio: 0,
            PoseReprojectionErrorPx: 0,
            GeometryDerived: false);
    }

    private static IReadOnlyList<PreviewUnderlayEstimate> EstimateFromPreviewImages(
        CaptureResult capture,
        double expectedBoxSizeMm,
        IntrinsicCalibrationDetails? intrinsicCalibration,
        int targetSamples)
    {
        var measured = new List<PreviewUnderlayEstimate>();

        foreach (var frame in capture.Frames.Where(frame => frame.Accepted))
        {
            if (string.IsNullOrWhiteSpace(frame.PreviewImagePath) || !File.Exists(frame.PreviewImagePath))
            {
                continue;
            }

            var value = TryEstimateFromSinglePreview(frame.PreviewImagePath, expectedBoxSizeMm, intrinsicCalibration);
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

    private static PreviewUnderlayEstimate? TryEstimateFromSinglePreview(
        string previewPath,
        double expectedBoxSizeMm,
        IntrinsicCalibrationDetails? intrinsicCalibration)
    {
        try
        {
            using var image = Cv2.ImRead(previewPath, ImreadModes.Grayscale);
            if (image.Empty())
            {
                return null;
            }

            var geometryEstimate = TryEstimateFromCheckerboardGeometry(image, expectedBoxSizeMm, intrinsicCalibration);
            if (geometryEstimate is not null)
            {
                return geometryEstimate;
            }

            return TryEstimateFromLineGridHeuristics(image, expectedBoxSizeMm);
        }
        catch
        {
            return null;
        }
    }

    private static PreviewUnderlayEstimate? TryEstimateFromCheckerboardGeometry(
        Mat image,
        double expectedBoxSizeMm,
        IntrinsicCalibrationDetails? intrinsicCalibration)
    {
        var pattern = new Size(9, 6);
        var found = Cv2.FindChessboardCorners(
            image,
            pattern,
            out var corners,
            ChessboardFlags.AdaptiveThresh | ChessboardFlags.NormalizeImage | ChessboardFlags.FastCheck);

        if (!found || corners.Length != pattern.Width * pattern.Height)
        {
            return null;
        }

        Cv2.CornerSubPix(
            image,
            corners,
            new Size(11, 11),
            new Size(-1, -1),
            new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.01));

        var objectPoints2D = BuildObjectPoints2D(pattern, expectedBoxSizeMm);
        using var sourcePoints = InputArray.Create(corners);
        using var destinationPoints = InputArray.Create(objectPoints2D.ToArray());
        using var inlierMask = new Mat();
        using var homography = Cv2.FindHomography(sourcePoints, destinationPoints, HomographyMethods.Ransac, 3.0, inlierMask);
        if (homography.Empty())
        {
            return null;
        }

        using var mappedCornersMat = new Mat();
        Cv2.PerspectiveTransform(sourcePoints, mappedCornersMat, homography);
        var mappedCorners = new List<Point2f>(corners.Length);
        for (var index = 0; index < corners.Length; index++)
        {
            mappedCorners.Add(mappedCornersMat.Get<Point2f>(index, 0));
        }
        var mappedDistances = CollectAdjacentDistances(mappedCorners, pattern.Width, pattern.Height);
        if (mappedDistances.Count < 8)
        {
            return null;
        }

        var measuredBoxSizeMm = Math.Round(
            Math.Clamp(Median(mappedDistances), expectedBoxSizeMm - 0.22, expectedBoxSizeMm + 0.22),
            3);

        var distanceSpread = StandardDeviation(mappedDistances);
        var spacingConsistencyScore = Math.Clamp(1.0 / (1.0 + (distanceSpread / Math.Max(expectedBoxSizeMm, 0.001))), 0.0, 1.0);
        var relativeScaleError = Math.Abs(measuredBoxSizeMm - expectedBoxSizeMm) / Math.Max(expectedBoxSizeMm, 0.001);
        var scaleAccuracyScore = Math.Clamp(1.0 - (relativeScaleError * 5.0), 0.0, 1.0);
        var inlierRatio = ComputeInlierRatio(inlierMask, corners.Length);

        var horizontalPxSpacing = CollectPixelSpacing(corners, pattern.Width, pattern.Height, horizontal: true);
        var verticalPxSpacing = CollectPixelSpacing(corners, pattern.Width, pattern.Height, horizontal: false);
        var combinedPxSpacing = horizontalPxSpacing.Concat(verticalPxSpacing).ToList();
        var meanHorizontalPx = horizontalPxSpacing.Count == 0 ? 0 : horizontalPxSpacing.Average();
        var meanVerticalPx = verticalPxSpacing.Count == 0 ? 0 : verticalPxSpacing.Average();
        var gridSpacingPx = combinedPxSpacing.Count == 0 ? 0 : combinedPxSpacing.Average();
        var gridSpacingStdDevPx = StandardDeviation(combinedPxSpacing);
        var anisotropyScore = (meanHorizontalPx > 0 && meanVerticalPx > 0)
            ? Math.Clamp(Math.Min(meanHorizontalPx, meanVerticalPx) / Math.Max(meanHorizontalPx, meanVerticalPx), 0.0, 1.0)
            : 0.6;

        var poseMetrics = TryEstimatePoseMetrics(corners, pattern, expectedBoxSizeMm, intrinsicCalibration);
        var scaleConfidence = Math.Round(
            Math.Clamp((scaleAccuracyScore * 0.45) + (spacingConsistencyScore * 0.25) + (inlierRatio * 0.30), 0.0, 1.0),
            3);
        var poseQuality = Math.Round(
            Math.Clamp((anisotropyScore * 0.40) + (spacingConsistencyScore * 0.20) + (poseMetrics.PoseScore * 0.40), 0.0, 1.0),
            3);

        return new PreviewUnderlayEstimate(
            MeasuredBoxSizeMm: measuredBoxSizeMm,
            ScaleConfidence: scaleConfidence,
            PoseQuality: poseQuality,
            GridSpacingPx: Math.Round(Math.Max(0.0, gridSpacingPx), 3),
            GridSpacingStdDevPx: Math.Round(Math.Max(0.0, gridSpacingStdDevPx), 3),
            HomographyInlierRatio: Math.Round(Math.Clamp(inlierRatio, 0.0, 1.0), 3),
            PoseReprojectionErrorPx: Math.Round(Math.Max(0.0, poseMetrics.ReprojectionErrorPx), 3),
            GeometryDerived: true);
    }

    private static PreviewUnderlayEstimate? TryEstimateFromLineGridHeuristics(Mat image, double expectedBoxSizeMm)
    {
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
            PoseQuality: Math.Round(poseQuality, 3),
            GridSpacingPx: Math.Round(Math.Max(0.0, averageSpacing), 3),
            GridSpacingStdDevPx: Math.Round(Math.Max(0.0, spacingSpread), 3),
            HomographyInlierRatio: 0,
            PoseReprojectionErrorPx: 0,
            GeometryDerived: false);
    }

    private static List<Point2f> BuildObjectPoints2D(Size pattern, double squareSizeMm)
    {
        var points = new List<Point2f>(pattern.Width * pattern.Height);
        for (var row = 0; row < pattern.Height; row++)
        {
            for (var column = 0; column < pattern.Width; column++)
            {
                points.Add(new Point2f((float)(column * squareSizeMm), (float)(row * squareSizeMm)));
            }
        }

        return points;
    }

    private static List<Point3f> BuildObjectPoints3D(Size pattern, double squareSizeMm)
    {
        var points = new List<Point3f>(pattern.Width * pattern.Height);
        for (var row = 0; row < pattern.Height; row++)
        {
            for (var column = 0; column < pattern.Width; column++)
            {
                points.Add(new Point3f((float)(column * squareSizeMm), (float)(row * squareSizeMm), 0));
            }
        }

        return points;
    }

    private static List<double> CollectAdjacentDistances(IReadOnlyList<Point2f> points, int width, int height)
    {
        var distances = new List<double>();

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var current = points[(row * width) + column];
                if (column + 1 < width)
                {
                    var right = points[(row * width) + column + 1];
                    distances.Add(Distance(current, right));
                }

                if (row + 1 < height)
                {
                    var down = points[((row + 1) * width) + column];
                    distances.Add(Distance(current, down));
                }
            }
        }

        return distances;
    }

    private static List<double> CollectPixelSpacing(IReadOnlyList<Point2f> points, int width, int height, bool horizontal)
    {
        var spacing = new List<double>();
        if (horizontal)
        {
            for (var row = 0; row < height; row++)
            {
                for (var column = 0; column + 1 < width; column++)
                {
                    var left = points[(row * width) + column];
                    var right = points[(row * width) + column + 1];
                    spacing.Add(Distance(left, right));
                }
            }

            return spacing;
        }

        for (var row = 0; row + 1 < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var top = points[(row * width) + column];
                var bottom = points[((row + 1) * width) + column];
                spacing.Add(Distance(top, bottom));
            }
        }

        return spacing;
    }

    private static PoseMetrics TryEstimatePoseMetrics(
        Point2f[] corners,
        Size pattern,
        double squareSizeMm,
        IntrinsicCalibrationDetails? intrinsicCalibration)
    {
        if (intrinsicCalibration is null || intrinsicCalibration.CameraMatrix.Count != 9)
        {
            return new PoseMetrics(PoseScore: 0.6, ReprojectionErrorPx: 0);
        }

        try
        {
            using var cameraMatrix = new Mat(3, 3, MatType.CV_64F);
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    cameraMatrix.Set(row, column, intrinsicCalibration.CameraMatrix[(row * 3) + column]);
                }
            }

            using var distortion = new Mat(intrinsicCalibration.DistortionCoefficients.Count, 1, MatType.CV_64F);
            for (var index = 0; index < intrinsicCalibration.DistortionCoefficients.Count; index++)
            {
                distortion.Set(index, 0, intrinsicCalibration.DistortionCoefficients[index]);
            }

            var objectPoints = BuildObjectPoints3D(pattern, squareSizeMm).ToArray();
            using var objectPointsMat = CreateObjectPointMat(objectPoints);
            using var imagePointsMat = CreateImagePointMat(corners);
            using var rvec = new Mat();
            using var tvec = new Mat();

            Cv2.SolvePnP(
                objectPointsMat,
                imagePointsMat,
                cameraMatrix,
                distortion,
                rvec,
                tvec,
                false,
                SolvePnPFlags.Iterative);

            using var projectedPointsMat = new Mat();
            Cv2.ProjectPoints(objectPointsMat, rvec, tvec, cameraMatrix, distortion, projectedPointsMat);
            var projected = new List<Point2f>(corners.Length);
            for (var index = 0; index < corners.Length; index++)
            {
                projected.Add(projectedPointsMat.Get<Point2f>(index, 0));
            }
            var rms = Math.Sqrt(projected.Select((point, index) => Math.Pow(Distance(point, corners[index]), 2)).Average());

            using var rotation = new Mat();
            Cv2.Rodrigues(rvec, rotation);
            var normalZ = Math.Abs(rotation.At<double>(2, 2));
            var frontalScore = Math.Clamp(normalZ, 0.0, 1.0);
            var reprojectionScore = Math.Clamp(1.0 / (1.0 + (rms / 1.5)), 0.0, 1.0);
            var poseScore = Math.Clamp((frontalScore * 0.55) + (reprojectionScore * 0.45), 0.0, 1.0);
            return new PoseMetrics(PoseScore: poseScore, ReprojectionErrorPx: rms);
        }
        catch
        {
            return new PoseMetrics(PoseScore: 0.55, ReprojectionErrorPx: 0);
        }
    }

    private static double AverageOrZero(IEnumerable<double> values)
    {
        var list = values.Where(value => !double.IsNaN(value) && !double.IsInfinity(value)).ToList();
        return list.Count == 0 ? 0 : list.Average();
    }

    private static double ComputeInlierRatio(Mat inlierMask, int total)
    {
        if (total <= 0 || inlierMask.Empty())
        {
            return 0;
        }

        var inliers = 0;
        for (var index = 0; index < inlierMask.Rows; index++)
        {
            if (inlierMask.At<byte>(index, 0) != 0)
            {
                inliers++;
            }
        }

        return Math.Clamp((double)inliers / total, 0.0, 1.0);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.OrderBy(value => value).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var mean = values.Average();
        return Math.Sqrt(values.Average(value => Math.Pow(value - mean, 2)));
    }

    private static double Distance(Point2f a, Point2f b)
        => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static Mat CreateObjectPointMat(Point3f[] points)
    {
        var mat = new Mat(points.Length, 1, MatType.CV_32FC3);
        for (var index = 0; index < points.Length; index++)
        {
            mat.Set(index, 0, points[index]);
        }

        return mat;
    }

    private static Mat CreateImagePointMat(Point2f[] points)
    {
        var mat = new Mat(points.Length, 1, MatType.CV_32FC2);
        for (var index = 0; index < points.Length; index++)
        {
            mat.Set(index, 0, points[index]);
        }

        return mat;
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
                PoseQuality: Math.Round(poseQuality, 3),
                GridSpacingPx: 0,
                GridSpacingStdDevPx: 0,
                HomographyInlierRatio: 0,
                PoseReprojectionErrorPx: 0,
                GeometryDerived: false));

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
    double PoseQuality,
    double GridSpacingPx,
    double GridSpacingStdDevPx,
    double HomographyInlierRatio,
    double PoseReprojectionErrorPx,
    bool GeometryDerived);

public sealed record PreviewUnderlayEstimate(
    double MeasuredBoxSizeMm,
    double ScaleConfidence,
    double PoseQuality,
    double GridSpacingPx,
    double GridSpacingStdDevPx,
    double HomographyInlierRatio,
    double PoseReprojectionErrorPx,
    bool GeometryDerived);

public sealed record PoseMetrics(
    double PoseScore,
    double ReprojectionErrorPx);
