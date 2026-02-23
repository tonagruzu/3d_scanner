namespace Scanner3D.Core.Models;

public sealed record CameraCaptureMode(
    int Width,
    int Height,
    int FramesPerSecond,
    string PixelFormat)
{
    public override string ToString() => $"{Width}x{Height}@{FramesPerSecond}fps/{PixelFormat}";
}
