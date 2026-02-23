using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.App;

public partial class MainWindow : Window
{
    private readonly IPipelineOrchestrator _pipelineOrchestrator;
    private string? _latestOutputDirectory;

    public MainWindow()
    {
        InitializeComponent();
        _pipelineOrchestrator = new Scanner3D.Pipeline.PipelineOrchestrator();
    }

    private async void RunPipelineStub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusTextBlock.Text = "Running pipeline...";
            ValidationSummaryTextBlock.Text = "Processing";
            ArtifactListBox.Items.Clear();
            OpenOutputButton.IsEnabled = false;
            _latestOutputDirectory = null;

            var session = new ScanSession(
                SessionId: Guid.NewGuid(),
                StartedAt: DateTimeOffset.UtcNow,
                CameraDeviceId: "bootstrap-device",
                OperatorNotes: "Initial pipeline shell execution");

            var result = await _pipelineOrchestrator.ExecuteAsync(session);

            StatusTextBlock.Text = result.Success ? "Completed (pass)" : "Completed (quality gate failed)";
            ValidationSummaryTextBlock.Text = result.Validation.Summary;

            AddArtifactListItem("Mesh", result.MeshPath);
            AddArtifactListItem("Validation", result.ValidationReportPath);
            foreach (var sketchPath in result.SketchPaths)
            {
                AddArtifactListItem("Sketch", sketchPath);
            }

            _latestOutputDirectory = Path.GetDirectoryName(result.MeshPath);
            OpenOutputButton.IsEnabled = !string.IsNullOrWhiteSpace(_latestOutputDirectory)
                                        && Directory.Exists(_latestOutputDirectory);
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = "Execution failed";
            ValidationSummaryTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, "Pipeline Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
}
