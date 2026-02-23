using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class OpenCvCameraModeProviderTests
{
    [Fact]
    public async Task GetSupportedModesAsync_InvalidDeviceId_ReturnsEmptyModes()
    {
        var provider = new OpenCvCameraModeProvider();

        var modes = await provider.GetSupportedModesAsync("invalid-device-id");

        Assert.Empty(modes);
    }
}
