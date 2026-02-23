using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;
using Scanner3D.Pipeline;

namespace Scanner3D.App;

public partial class MainWindow : Window
{
    private const string GuiVersion = "v2.0.0";

    private sealed record CameraOption(string DeviceId, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record RunHistoryEntry(DateTimeOffset Timestamp, PipelineResult Result);

    private readonly IPipelineOrchestrator _pipelineOrchestrator;
    private readonly ICameraDeviceDiscovery _cameraDeviceDiscovery;
    private readonly List<RunHistoryEntry> _runHistory = [];
    private bool _isRunning;
    private CancellationTokenSource? _runCancellationTokenSource;
    private PipelineResult? _latestResult;
    private string? _latestSummaryFilePath;
    private string? _latestOutputDirectory;
    private string _selectedCameraDeviceId = "bootstrap-device";
    private readonly string _previewDirectory = Path.Combine(Path.GetTempPath(), "scanner3d-preview");
    private DispatcherTimer? _livePreviewTimer;
    private string? _lastLivePreviewPath;
    private DateTimeOffset? _lastLivePreviewTimestamp;
    private DateTimeOffset? _previousLivePreviewTimestamp;

    public MainWindow()
    {
        InitializeComponent();
        GuiVersionTextBlock.Text = $"GUI {GuiVersion}";
        Title = $"3D Scanner ({GuiVersion})";
        _pipelineOrchestrator = new Scanner3D.Pipeline.PipelineOrchestrator();
        _cameraDeviceDiscovery = CreateDefaultDeviceDiscovery();
        Loaded += MainWindow_Loaded;
        UpdateActionStates();
    }

    private static ICameraDeviceDiscovery CreateDefaultDeviceDiscovery()
    {
        return OperatingSystem.IsWindows()
            ? new CompositeCameraDeviceDiscovery(
                new WindowsCameraDeviceDiscovery(),
                new CompositeCameraDeviceDiscovery(new OpenCvCameraDeviceDiscovery(), new MockCameraDeviceDiscovery()))
            : new MockCameraDeviceDiscovery();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await ReloadCameraDevicesAsync();
    }

    private async Task ReloadCameraDevicesAsync()
    {
        var currentSelection = CameraPickerComboBox.SelectedValue as string;

        try
        {
            CameraPickerComboBox.Items.Clear();

            var devices = await _cameraDeviceDiscovery.GetAvailableDevicesAsync();
            var availableDevices = devices.Where(device => device.IsAvailable).ToList();

            if (availableDevices.Count == 0)
            {
                var fallback = new CameraOption("bootstrap-device", "No camera detected (fallback)");
                CameraPickerComboBox.Items.Add(fallback);
                CameraPickerComboBox.SelectedItem = fallback;
                _selectedCameraDeviceId = fallback.DeviceId;
                UpdateSelectedCameraText();
                return;
            }

            foreach (var device in availableDevices)
            {
                CameraPickerComboBox.Items.Add(new CameraOption(device.DeviceId, device.DisplayName));
            }

            var preferredSelection = availableDevices.FirstOrDefault(device =>
                string.Equals(device.DeviceId, currentSelection, StringComparison.OrdinalIgnoreCase))?.DeviceId
                ?? availableDevices.FirstOrDefault(device =>
                    string.Equals(device.DeviceId, _selectedCameraDeviceId, StringComparison.OrdinalIgnoreCase))?.DeviceId
                ?? availableDevices[0].DeviceId;

            CameraPickerComboBox.SelectedValue = preferredSelection;
            _selectedCameraDeviceId = preferredSelection;
            UpdateSelectedCameraText();
        }
        catch (Exception exception)
        {
            CameraPickerComboBox.Items.Clear();
            var fallback = new CameraOption("bootstrap-device", "Camera discovery failed (fallback)");
            CameraPickerComboBox.Items.Add(fallback);
            CameraPickerComboBox.SelectedItem = fallback;
            _selectedCameraDeviceId = fallback.DeviceId;
            StatusTextBlock.Text = "Camera discovery failed";
            ValidationSummaryTextBlock.Text = exception.Message;
            UpdateSelectedCameraText();
        }
    }

    private async void RefreshCamerasButton_Click(object sender, RoutedEventArgs e)
    {
        await ReloadCameraDevicesAsync();
    }

    private void CameraPickerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CameraPickerComboBox.SelectedItem is CameraOption option)
        {
            _selectedCameraDeviceId = option.DeviceId;
            UpdateSelectedCameraText();
        }
    }

    private void UpdateSelectedCameraText()
    {
        if (CameraPickerComboBox.SelectedItem is CameraOption option)
        {
            SelectedCameraTextBlock.Text = $"Selected: {option.DisplayName}";
            return;
        }

        SelectedCameraTextBlock.Text = "Selected: (none)";
    }

    private async void RunPipelineStub_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        UpdateActionStates();

        try
        {
            StatusTextBlock.Text = "Running pipeline...";
            ValidationSummaryTextBlock.Text = "Processing";
            PreflightSummaryTextBlock.Text = "Processing";
            CalibrationSummaryTextBlock.Text = "Processing";
            UnderlaySummaryTextBlock.Text = "Processing";
            ClearFramePreview();
            StartLivePreviewPolling();
            ArtifactListBox.Items.Clear();
            _latestResult = null;
            _latestSummaryFilePath = null;
            _latestOutputDirectory = null;
            UpdateActionStates();

            _runCancellationTokenSource = new CancellationTokenSource();

            var session = new ScanSession(
                SessionId: Guid.NewGuid(),
                StartedAt: DateTimeOffset.UtcNow,
                CameraDeviceId: _selectedCameraDeviceId,
                OperatorNotes: "Initial pipeline shell execution");

            var result = await _pipelineOrchestrator.ExecuteAsync(session, _runCancellationTokenSource.Token);

            _latestOutputDirectory = Path.GetDirectoryName(result.MeshPath);
            _latestResult = result;

            var historyEntry = new RunHistoryEntry(DateTimeOffset.Now, result);
            _runHistory.Add(historyEntry);
            AddRunHistoryListItem(historyEntry);
            DisplayResult(result);
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Cancelled";
            ValidationSummaryTextBlock.Text = "Pipeline execution cancelled by user.";
            PreflightSummaryTextBlock.Text = "Cancelled";
            CalibrationSummaryTextBlock.Text = "Cancelled";
            UnderlaySummaryTextBlock.Text = "Cancelled";
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = "Execution failed";
            ValidationSummaryTextBlock.Text = exception.Message;
            PreflightSummaryTextBlock.Text = exception.Message.Contains("preflight", StringComparison.OrdinalIgnoreCase)
                ? exception.Message
                : "Not available";
            CalibrationSummaryTextBlock.Text = "Not available";
            UnderlaySummaryTextBlock.Text = "Not available";
            MessageBox.Show(exception.Message, "Pipeline Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            StopLivePreviewPolling();
            _runCancellationTokenSource?.Dispose();
            _runCancellationTokenSource = null;
            _isRunning = false;
            UpdateActionStates();
        }
    }

    private void CancelRunButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning)
        {
            return;
        }

        _runCancellationTokenSource?.Cancel();
        StatusTextBlock.Text = "Cancelling...";
    }

    private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_latestOutputDirectory) || !Directory.Exists(_latestOutputDirectory))
        {
            MessageBox.Show("No output folder is available yet. Run the pipeline first.", "Output Folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _latestOutputDirectory,
            UseShellExecute = true
        });
    }

    private void ArtifactListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ArtifactListBox.SelectedItem is not ListBoxItem listItem || listItem.Tag is not string artifactPath)
        {
            return;
        }

        if (!File.Exists(artifactPath))
        {
            MessageBox.Show($"File not found: {artifactPath}", "Open Artifact", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = artifactPath,
            UseShellExecute = true
        });
    }

    private void AddArtifactListItem(string artifactType, string artifactPath)
    {
        var item = new ListBoxItem
        {
            Content = $"{artifactType}: {Path.GetFileName(artifactPath)}",
            Tag = artifactPath
        };

        ArtifactListBox.Items.Add(item);
    }

    private void AddRunHistoryListItem(RunHistoryEntry entry)
    {
        var item = new ListBoxItem
        {
            Content = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} | {(entry.Result.Success ? "PASS" : "FAIL")}",
            Tag = entry
        };

        RunHistoryListBox.Items.Add(item);
        RunHistoryListBox.SelectedItem = item;
    }

    private void DisplayResult(PipelineResult result)
    {
        StatusTextBlock.Text = result.Success ? "Completed (pass)" : "Completed (quality gate failed)";
        ValidationSummaryTextBlock.Text = result.Validation.Summary;
        PreflightSummaryTextBlock.Text = BuildPreflightUiSummary(result.CapturePreflight, result.Capture);
        CalibrationSummaryTextBlock.Text = BuildCalibrationUiSummary(result.Calibration, result.CalibrationQuality);
        UnderlaySummaryTextBlock.Text = BuildUnderlayUiSummary(result.UnderlayVerification);
        DisplayFramePreview(result.Capture);

        ArtifactListBox.Items.Clear();
        AddArtifactListItem("Mesh", result.MeshPath);
        AddArtifactListItem("Validation", result.ValidationReportPath);
        foreach (var sketchPath in result.SketchPaths)
        {
            AddArtifactListItem("Sketch", sketchPath);
        }

        _latestOutputDirectory = Path.GetDirectoryName(result.MeshPath);
        UpdateActionStates();
    }

    private void CopyRunSummary_Click(object sender, RoutedEventArgs e)
    {
        if (_latestResult is null)
        {
            MessageBox.Show("No run summary is available yet. Run the pipeline first.", "Copy Run Summary", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var summary = BuildRunSummary(_latestResult);
        Clipboard.SetText(summary);
        MessageBox.Show("Run summary copied to clipboard.", "Copy Run Summary", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string BuildRunSummary(PipelineResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Scanner3D Run Summary (GUI {GuiVersion})");
        builder.AppendLine($"Status: {(result.Success ? "PASS" : "FAIL")}");
        builder.AppendLine($"Message: {result.Message}");
        builder.AppendLine();

        builder.AppendLine("Preflight");
        builder.AppendLine($"- Summary: {result.CapturePreflight?.Summary ?? "Not available"}");
        builder.AppendLine($"- Backend candidate: {result.CapturePreflight?.BackendCandidate ?? result.Capture.CaptureBackend}");
        if (result.CapturePreflight is not null)
        {
            builder.AppendLine($"- Pass: {result.CapturePreflight.Pass}");
            builder.AppendLine($"- Timestamp readiness: {result.CapturePreflight.TimestampReadinessPass}");
            builder.AppendLine($"- Exposure lock capability: {result.CapturePreflight.ExposureLockCapabilityStatus}");
            builder.AppendLine($"- White-balance lock capability: {result.CapturePreflight.WhiteBalanceLockCapabilityStatus}");

            if (result.CapturePreflight.BlockingIssues.Count > 0)
            {
                foreach (var issue in result.CapturePreflight.BlockingIssues)
                {
                    builder.AppendLine($"- Blocking: {issue}");
                }
            }

            if (result.CapturePreflight.Warnings.Count > 0)
            {
                foreach (var warning in result.CapturePreflight.Warnings)
                {
                    builder.AppendLine($"- Warning: {warning}");
                }
            }
        }
        builder.AppendLine();

        builder.AppendLine("Capture");
        builder.AppendLine($"- Camera: {result.Capture.CameraDeviceId}");
        builder.AppendLine($"- Frames: {result.Capture.AcceptedFrameCount}/{result.Capture.CapturedFrameCount} accepted");
        builder.AppendLine($"- Backend: {result.Capture.CaptureBackend}");
        builder.AppendLine($"- Exposure lock status: {result.Capture.ExposureLockStatus}");
        builder.AppendLine($"- White-balance lock status: {result.Capture.WhiteBalanceLockStatus}");
        builder.AppendLine();

        builder.AppendLine("Calibration");
        builder.AppendLine($"- Profile: {result.Calibration.CalibrationProfileId}");
        builder.AppendLine($"- Reprojection error (px): {result.Calibration.ReprojectionErrorPx:0.###}");
        builder.AppendLine($"- Scale error (mm): {result.Calibration.ScaleErrorMm:0.###}");
        builder.AppendLine($"- Gate pass: {result.CalibrationQuality.GatePass}");
        builder.AppendLine($"- Intrinsic frames used: {result.CalibrationQuality.UsedIntrinsicFrames}/{result.CalibrationQuality.MinimumRequiredIntrinsicFrames}");
        builder.AppendLine($"- Underlay scale confidence: {result.CalibrationQuality.UnderlayScaleConfidence:0.###} (min {CalibrationGateThresholds.MinUnderlayScaleConfidence:0.###})");
        builder.AppendLine($"- Underlay pose quality: {result.CalibrationQuality.UnderlayPoseQuality:0.###} (min {CalibrationGateThresholds.MinUnderlayPoseQuality:0.###})");
        if (result.CalibrationQuality.GateFailures.Count > 0)
        {
            foreach (var failure in result.CalibrationQuality.GateFailures)
            {
                builder.AppendLine($"- Calibration gate failure: {failure}");
            }
        }
        if (result.Calibration.IntrinsicCalibration is not null)
        {
            var intrinsic = result.Calibration.IntrinsicCalibration;
            builder.AppendLine("- Intrinsic calibration: available");
            builder.AppendLine($"- Pattern: {intrinsic.PatternType} {intrinsic.PatternColumns}x{intrinsic.PatternRows} @ {intrinsic.SquareSizeMm:0.###} mm");
            builder.AppendLine($"- Image size (px): {intrinsic.ImageWidthPx}x{intrinsic.ImageHeightPx}");
            builder.AppendLine($"- Used frames: {intrinsic.UsedFrameIds.Count}; rejected: {intrinsic.RejectedFrameReasons.Count}");
            if (intrinsic.CameraMatrix.Count >= 9)
            {
                builder.AppendLine($"- Intrinsics fx/fy/cx/cy: {intrinsic.CameraMatrix[0]:0.###}/{intrinsic.CameraMatrix[4]:0.###}/{intrinsic.CameraMatrix[2]:0.###}/{intrinsic.CameraMatrix[5]:0.###}");
            }
            builder.AppendLine($"- Distortion coeff count: {intrinsic.DistortionCoefficients.Count}");
        }
        else
        {
            builder.AppendLine("- Intrinsic calibration: unavailable (fallback mode)");
        }
        builder.AppendLine();

        builder.AppendLine("Underlay");
        builder.AppendLine($"- Pattern: {result.UnderlayVerification.UnderlayPatternId}");
        builder.AppendLine($"- Detection mode: {result.UnderlayVerification.DetectionMode}");
        builder.AppendLine($"- Expected box size (mm): {result.UnderlayVerification.ExpectedBoxSizeMm:0.###}");
        builder.AppendLine($"- Inlier samples: {result.UnderlayVerification.InlierBoxSizesMm.Count}/{result.UnderlayVerification.MeasuredBoxSizesMm.Count}");
        builder.AppendLine($"- Fit confidence: {result.UnderlayVerification.FitConfidence:0.###}");
        builder.AppendLine($"- Scale confidence: {result.UnderlayVerification.ScaleConfidence:0.###}");
        builder.AppendLine($"- Pose quality: {result.UnderlayVerification.PoseQuality:0.###}");
        builder.AppendLine($"- Max box error (mm): {result.UnderlayVerification.MaxAbsoluteErrorMm:0.###}");
        builder.AppendLine();

        builder.AppendLine("Validation");
        builder.AppendLine($"- Summary: {result.Validation.Summary}");
        builder.AppendLine($"- Max absolute error (mm): {result.Validation.MaxAbsoluteErrorMm:0.###}");
        builder.AppendLine($"- Mean absolute error (mm): {result.Validation.MeanAbsoluteErrorMm:0.###}");
        builder.AppendLine();

        builder.AppendLine("Artifacts");
        builder.AppendLine($"- Mesh: {result.MeshPath}");
        builder.AppendLine($"- Validation report: {result.ValidationReportPath}");
        foreach (var sketchPath in result.SketchPaths)
        {
            builder.AppendLine($"- Sketch: {sketchPath}");
        }

        return builder.ToString();
    }

    private void RunHistoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RunHistoryListBox.SelectedItem is not ListBoxItem listItem || listItem.Tag is not RunHistoryEntry entry)
        {
            return;
        }

        _latestResult = entry.Result;
        DisplayResult(entry.Result);
    }

    private static string BuildPreflightUiSummary(CapturePreflightResult? preflight, CaptureResult capture)
    {
        if (preflight is null)
        {
            return $"Backend={capture.CaptureBackend}; preflight details unavailable";
        }

        var status = preflight.Pass ? "PASS" : "FAIL";
        var warningCount = preflight.Warnings.Count;
        var issueCount = preflight.BlockingIssues.Count;
        return $"{status} | backend={preflight.BackendCandidate} | warnings={warningCount} | blocking={issueCount}";
    }

    private static string BuildCalibrationUiSummary(CalibrationResult calibration)
    {
        var intrinsic = calibration.IntrinsicCalibration;
        if (intrinsic is null)
        {
            return $"reproj={calibration.ReprojectionErrorPx:0.###} px | scale={calibration.ScaleErrorMm:0.###} mm | intrinsic=fallback";
        }

        return $"reproj={calibration.ReprojectionErrorPx:0.###} px | scale={calibration.ScaleErrorMm:0.###} mm | intrinsic={intrinsic.PatternType} | used={intrinsic.UsedFrameIds.Count} rejected={intrinsic.RejectedFrameReasons.Count}";
    }

    private static string BuildCalibrationUiSummary(CalibrationResult calibration, CalibrationQualitySummary calibrationQuality)
    {
        var intrinsicSummary = BuildCalibrationUiSummary(calibration);
        var gateStatus = calibrationQuality.GatePass ? "gate=PASS" : "gate=FAIL";
        var gateDetails = calibrationQuality.GateFailures.Count == 0
            ? string.Empty
            : $" | issues={string.Join(", ", calibrationQuality.GateFailures)}";

        return $"{intrinsicSummary} | {gateStatus}{gateDetails}";
    }

    private static string BuildUnderlayUiSummary(UnderlayVerificationResult underlay)
    {
        return $"mode={underlay.DetectionMode} | fit={underlay.FitConfidence:0.###} | scale={underlay.ScaleConfidence:0.###} | pose={underlay.PoseQuality:0.###} | inliers={underlay.InlierBoxSizesMm.Count}/{underlay.MeasuredBoxSizesMm.Count} | maxErr={underlay.MaxAbsoluteErrorMm:0.###} mm";
    }

    private void DisplayFramePreview(CaptureResult capture)
    {
        var latestPreviewFrame = capture.Frames
            .Where(frame => !string.IsNullOrWhiteSpace(frame.PreviewImagePath) && File.Exists(frame.PreviewImagePath))
            .OrderBy(frame => frame.CapturedAt)
            .LastOrDefault();

        if (latestPreviewFrame is null || string.IsNullOrWhiteSpace(latestPreviewFrame.PreviewImagePath))
        {
            ClearFramePreview();
            FramePreviewStatusTextBlock.Text = "No frame preview file was captured for this run.";
            FrameQualityMetricsTextBlock.Text = BuildRequiredThresholdText();
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(latestPreviewFrame.PreviewImagePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            FramePreviewImage.Source = bitmap;
            FramePreviewPlaceholderTextBlock.Visibility = Visibility.Collapsed;
            FramePreviewStatusTextBlock.Text = $"Latest frame: {latestPreviewFrame.FrameId} ({(latestPreviewFrame.Accepted ? "accepted" : "rejected")})";
            FrameQualityMetricsTextBlock.Text = BuildFrameQualityText(latestPreviewFrame);
        }
        catch
        {
            ClearFramePreview();
            FramePreviewStatusTextBlock.Text = "Preview image could not be loaded.";
            FrameQualityMetricsTextBlock.Text = BuildRequiredThresholdText();
        }
    }

    private void ClearFramePreview()
    {
        FramePreviewImage.Source = null;
        FramePreviewPlaceholderTextBlock.Visibility = Visibility.Visible;
        FramePreviewStatusTextBlock.Text = string.Empty;
        FrameQualityMetricsTextBlock.Text = BuildRequiredThresholdText();
    }

    private void RefreshPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshPreviewFromDisk(isLiveTick: false);
    }

    private void StartLivePreviewPolling()
    {
        _lastLivePreviewPath = null;
        _lastLivePreviewTimestamp = null;
        _previousLivePreviewTimestamp = null;

        _livePreviewTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        _livePreviewTimer.Tick -= LivePreviewTimer_Tick;
        _livePreviewTimer.Tick += LivePreviewTimer_Tick;
        _livePreviewTimer.Start();
    }

    private void StopLivePreviewPolling()
    {
        if (_livePreviewTimer is null)
        {
            return;
        }

        _livePreviewTimer.Stop();
        _livePreviewTimer.Tick -= LivePreviewTimer_Tick;
    }

    private void LivePreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isRunning)
        {
            return;
        }

        RefreshPreviewFromDisk(isLiveTick: true);
    }

    private void RefreshPreviewFromDisk(bool isLiveTick)
    {
        try
        {
            if (!Directory.Exists(_previewDirectory))
            {
                if (!isLiveTick)
                {
                    FramePreviewStatusTextBlock.Text = "No preview files found yet.";
                }

                return;
            }

            var latestPreviewPath = Directory
                .EnumerateFiles(_previewDirectory, "*.jpg", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => new { file.FullName, file.LastWriteTimeUtc })
                .FirstOrDefault();

            if (latestPreviewPath is null || string.IsNullOrWhiteSpace(latestPreviewPath.FullName))
            {
                if (!isLiveTick)
                {
                    FramePreviewStatusTextBlock.Text = "No preview files found yet.";
                }

                return;
            }

            if (isLiveTick && string.Equals(latestPreviewPath.FullName, _lastLivePreviewPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(latestPreviewPath.FullName, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            FramePreviewImage.Source = bitmap;
            FramePreviewPlaceholderTextBlock.Visibility = Visibility.Collapsed;

            var now = DateTimeOffset.UtcNow;
            _previousLivePreviewTimestamp = _lastLivePreviewTimestamp;
            _lastLivePreviewTimestamp = now;

            var frameWriteTime = new DateTimeOffset(DateTime.SpecifyKind(latestPreviewPath.LastWriteTimeUtc, DateTimeKind.Utc));
            var age = DateTimeOffset.UtcNow - frameWriteTime;
            var fpsText = "n/a";
            if (_previousLivePreviewTimestamp.HasValue)
            {
                var deltaSeconds = Math.Max(0.001, (_lastLivePreviewTimestamp.Value - _previousLivePreviewTimestamp.Value).TotalSeconds);
                fpsText = $"{(1.0 / deltaSeconds):0.0}";
            }

            FramePreviewStatusTextBlock.Text = isLiveTick
                ? $"Live preview | age={age.TotalMilliseconds:0} ms | update~{fpsText} fps"
                : $"Manual refresh | age={age.TotalMilliseconds:0} ms";

            if (_latestResult is not null)
            {
                var currentFrame = _latestResult.Capture.Frames
                    .FirstOrDefault(frame => string.Equals(frame.PreviewImagePath, latestPreviewPath.FullName, StringComparison.OrdinalIgnoreCase));

                FrameQualityMetricsTextBlock.Text = currentFrame is null
                    ? BuildRequiredThresholdText()
                    : BuildFrameQualityText(currentFrame);
            }
            else
            {
                FrameQualityMetricsTextBlock.Text = $"Live metrics pending. {BuildRequiredThresholdText()}";
            }

            _lastLivePreviewPath = latestPreviewPath.FullName;
        }
        catch
        {
            if (!isLiveTick)
            {
                FramePreviewStatusTextBlock.Text = "Preview image could not be loaded.";
                FrameQualityMetricsTextBlock.Text = BuildRequiredThresholdText();
            }
        }
    }

    private static string BuildFrameQualityText(CaptureFrame frame)
    {
        return $"Sharpness={frame.SharpnessScore:0.000} (min {CaptureQualityThresholds.SharpnessMinForAcceptance:0.00}) | "
             + $"Exposure={frame.ExposureScore:0.000} (min {CaptureQualityThresholds.ExposureMinForAcceptance:0.00})";
    }

    private static string BuildRequiredThresholdText()
    {
        return $"Required for pass: sharpness ≥ {CaptureQualityThresholds.SharpnessMinForAcceptance:0.00}, "
             + $"exposure ≥ {CaptureQualityThresholds.ExposureMinForAcceptance:0.00}";
    }

    private void ExportRunSummary_Click(object sender, RoutedEventArgs e)
    {
        if (_latestResult is null)
        {
            MessageBox.Show("No run summary is available yet. Run the pipeline first.", "Export Run Summary", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_latestOutputDirectory) || !Directory.Exists(_latestOutputDirectory))
        {
            MessageBox.Show("No output folder is available for export.", "Export Run Summary", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var summary = BuildRunSummary(_latestResult);
        var fileName = $"run-summary-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.txt";
        var filePath = Path.Combine(_latestOutputDirectory, fileName);

        File.WriteAllText(filePath, summary, Encoding.UTF8);
        _latestSummaryFilePath = filePath;
        UpdateActionStates();
        MessageBox.Show($"Run summary exported to:\n{filePath}", "Export Run Summary", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenLastSummaryFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_latestSummaryFilePath) || !File.Exists(_latestSummaryFilePath))
        {
            MessageBox.Show("No exported summary file is available yet.", "Open Last Summary File", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateActionStates();
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _latestSummaryFilePath,
            UseShellExecute = true
        });
    }

    private void UpdateActionStates()
    {
        var hasOutputDirectory = !string.IsNullOrWhiteSpace(_latestOutputDirectory) && Directory.Exists(_latestOutputDirectory);
        var hasResult = _latestResult is not null;
        var hasSummaryFile = !string.IsNullOrWhiteSpace(_latestSummaryFilePath) && File.Exists(_latestSummaryFilePath);

        RunPipelineButton.IsEnabled = !_isRunning;
        CancelRunButton.IsEnabled = _isRunning;
        OpenOutputButton.IsEnabled = !_isRunning && hasOutputDirectory;
        CopySummaryButton.IsEnabled = !_isRunning && hasResult;
        ExportSummaryButton.IsEnabled = !_isRunning && hasOutputDirectory && hasResult;
        OpenSummaryButton.IsEnabled = !_isRunning && hasSummaryFile;
        RunHistoryListBox.IsEnabled = !_isRunning;
        ArtifactListBox.IsEnabled = !_isRunning;
        CameraPickerComboBox.IsEnabled = !_isRunning;
        RefreshCamerasButton.IsEnabled = !_isRunning;
    }
}
