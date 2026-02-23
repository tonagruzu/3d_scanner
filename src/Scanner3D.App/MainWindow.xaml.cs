using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.App;

public partial class MainWindow : Window
{
    private sealed record RunHistoryEntry(DateTimeOffset Timestamp, PipelineResult Result);

    private readonly IPipelineOrchestrator _pipelineOrchestrator;
    private readonly List<RunHistoryEntry> _runHistory = [];
    private bool _isRunning;
    private CancellationTokenSource? _runCancellationTokenSource;
    private PipelineResult? _latestResult;
    private string? _latestSummaryFilePath;
    private string? _latestOutputDirectory;

    public MainWindow()
    {
        InitializeComponent();
        _pipelineOrchestrator = new Scanner3D.Pipeline.PipelineOrchestrator();
        UpdateActionStates();
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
            ArtifactListBox.Items.Clear();
            _latestResult = null;
            _latestSummaryFilePath = null;
            _latestOutputDirectory = null;
            UpdateActionStates();

            _runCancellationTokenSource = new CancellationTokenSource();

            var session = new ScanSession(
                SessionId: Guid.NewGuid(),
                StartedAt: DateTimeOffset.UtcNow,
                CameraDeviceId: "bootstrap-device",
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
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = "Execution failed";
            ValidationSummaryTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, "Pipeline Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
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
        builder.AppendLine("Scanner3D Run Summary");
        builder.AppendLine($"Status: {(result.Success ? "PASS" : "FAIL")}");
        builder.AppendLine($"Message: {result.Message}");
        builder.AppendLine();

        builder.AppendLine("Capture");
        builder.AppendLine($"- Camera: {result.Capture.CameraDeviceId}");
        builder.AppendLine($"- Frames: {result.Capture.AcceptedFrameCount}/{result.Capture.CapturedFrameCount} accepted");
        builder.AppendLine();

        builder.AppendLine("Calibration");
        builder.AppendLine($"- Profile: {result.Calibration.CalibrationProfileId}");
        builder.AppendLine($"- Reprojection error (px): {result.Calibration.ReprojectionErrorPx:0.###}");
        builder.AppendLine($"- Scale error (mm): {result.Calibration.ScaleErrorMm:0.###}");
        builder.AppendLine();

        builder.AppendLine("Underlay");
        builder.AppendLine($"- Pattern: {result.UnderlayVerification.UnderlayPatternId}");
        builder.AppendLine($"- Expected box size (mm): {result.UnderlayVerification.ExpectedBoxSizeMm:0.###}");
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
    }
}
