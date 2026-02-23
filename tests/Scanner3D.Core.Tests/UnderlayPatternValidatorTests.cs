using Scanner3D.Pipeline;
using Xunit;

namespace Scanner3D.Core.Tests;

public class UnderlayPatternValidatorTests
{
    [Fact]
    public void Validate_RejectsOutliers_AndComputesConfidence()
    {
        var validator = new UnderlayPatternValidator();
        var measured = new List<double> { 9.98, 10.02, 10.01, 9.99, 10.00, 11.20 };

        var result = validator.Validate("Mata-10mm-grid", "preview-image", 10.0, measured, toleranceMm: 0.2);

        Assert.True(result.Performed);
        Assert.True(result.Pass);
        Assert.Equal("preview-image", result.DetectionMode);
        Assert.True(result.InlierBoxSizesMm.Count < result.MeasuredBoxSizesMm.Count);
        Assert.DoesNotContain(result.InlierBoxSizesMm, value => value > 10.5);
        Assert.InRange(result.FitConfidence, 0.0, 1.0);
        Assert.True(result.FitConfidence > 0.6);
    }

    [Fact]
    public void Validate_ReturnsZeroConfidence_WhenNoSamples()
    {
        var validator = new UnderlayPatternValidator();

        var result = validator.Validate("Mata-10mm-grid", "static-fallback", 10.0, [], toleranceMm: 0.2);

        Assert.False(result.Performed);
        Assert.False(result.Pass);
        Assert.Equal("static-fallback", result.DetectionMode);
        Assert.Equal(0.0, result.FitConfidence);
        Assert.Empty(result.InlierBoxSizesMm);
    }
}
