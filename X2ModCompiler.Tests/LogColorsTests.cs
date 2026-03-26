using Xunit;
using Kokuban;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Tests;

public class LogColorsTests
{
    [Fact]
    public void Error_ReturnsFormattedString()
    {
        // Arrange
        var message = "Test error message";

        // Act
        var result = LogColors.Error(message);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test error message", result.ToString());
    }

    [Fact]
    public void Warning_ReturnsFormattedString()
    {
        // Arrange
        var message = "Test warning message";

        // Act
        var result = LogColors.Warning(message);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test warning message", result.ToString());
    }

    [Fact]
    public void Info_ReturnsFormattedString()
    {
        // Arrange
        var message = "Test info message";

        // Act
        var result = LogColors.Info(message);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test info message", result.ToString());
    }

    [Fact]
    public void Success_ReturnsFormattedString()
    {
        // Arrange
        var message = "Test success message";

        // Act
        var result = LogColors.Success(message);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test success message", result.ToString());
    }

    [Fact]
    public void Debug_ReturnsFormattedString()
    {
        // Arrange
        var message = "Test debug message";

        // Act
        var result = LogColors.Debug(message);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test debug message", result.ToString());
    }

    [Fact]
    public void StepHeader_ReturnsFormattedString()
    {
        // Arrange
        var stepName = "Test Step";

        // Act
        var result = LogColors.StepHeader(stepName);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(">>> STARTING STEP: Test Step", result.ToString());
    }

    [Fact]
    public void PhaseHeader_ReturnsFormattedString()
    {
        // Arrange
        var phaseName = "Phase 1";

        // Act
        var result = LogColors.PhaseHeader(phaseName);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Phase 1", result.ToString());
    }

    [Fact]
    public void ModeIndicator_ReturnsFormattedString()
    {
        // Arrange
        var mode = "Two-Pass";

        // Act
        var result = LogColors.ModeIndicator(mode);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("[MODE] Two-Pass", result.ToString());
    }

    [Fact]
    public void PathInfo_ReturnsFormattedString()
    {
        // Arrange
        var path = "C:\\Test\\Path";

        // Act
        var result = LogColors.PathInfo(path);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Target path: C:\\Test\\Path", result.ToString());
    }

    [Fact]
    public void PackageName_ReturnsFormattedString()
    {
        // Arrange
        var pkg = "TestPackage";

        // Act
        var result = LogColors.PackageName(pkg);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("-> TestPackage", result.ToString());
    }
}
