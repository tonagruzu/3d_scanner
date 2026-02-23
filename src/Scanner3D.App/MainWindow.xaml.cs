using System.Windows;
using Scanner3D.Core.Models;
using Scanner3D.Core.Services;

namespace Scanner3D.App;

public partial class MainWindow : Window
{
    private readonly IPipelineOrchestrator _pipelineOrchestrator;

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

            var session = new ScanSession(
                SessionId: Guid.NewGuid(),
                StartedAt: DateTimeOffset.UtcNow,
                CameraDeviceId: "bootstrap-device",
                OperatorNotes: "Initial pipeline shell execution");

            var result = await _pipelineOrchestrator.ExecuteAsync(session);

            StatusTextBlock.Text = result.Success ? "Completed (pass)" : "Completed (quality gate failed)";
            ValidationSummaryTextBlock.Text = result.Validation.Summary;

            ArtifactListBox.Items.Add($"Mesh: {result.MeshPath}");
            ArtifactListBox.Items.Add($"Validation: {result.ValidationReportPath}");
            foreach (var sketchPath in result.SketchPaths)
            {
                ArtifactListBox.Items.Add($"Sketch: {sketchPath}");
            }
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = "Execution failed";
            ValidationSummaryTextBlock.Text = exception.Message;
            MessageBox.Show(exception.Message, "Pipeline Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
