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
        var session = new ScanSession(
            SessionId: Guid.NewGuid(),
            StartedAt: DateTimeOffset.UtcNow,
            CameraDeviceId: "bootstrap-device",
            OperatorNotes: "Initial pipeline shell execution");

        var result = await _pipelineOrchestrator.ExecuteAsync(session);
        MessageBox.Show(result.Message, "Pipeline Result", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
