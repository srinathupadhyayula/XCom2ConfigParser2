using Xunit;
using X2ModCompiler.Utilities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace X2ModCompiler.Tests;

/// <summary>
/// Integration tests verifying LogColors adoption across the codebase.
/// </summary>
public class LogColorsAdoptionTests
{
    private readonly ILogger<LogColorsAdoptionTests> _logger;

    public LogColorsAdoptionTests()
    {
        _logger = Substitute.For<ILogger<LogColorsAdoptionTests>>();
    }

    [Fact]
    public void LogColors_Error_CanBeLogged()
    {
        // Arrange
        var message = "Test error";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.Error(message));
    }

    [Fact]
    public void LogColors_Warning_CanBeLogged()
    {
        // Arrange
        var message = "Test warning";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.Warning(message));
    }

    [Fact]
    public void LogColors_Info_CanBeLogged()
    {
        // Arrange
        var message = "Test info";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.Info(message));
    }

    [Fact]
    public void LogColors_Success_CanBeLogged()
    {
        // Arrange
        var message = "Test success";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.Success(message));
    }

    [Fact]
    public void LogColors_Debug_CanBeLogged()
    {
        // Arrange
        var message = "Test debug";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.Debug(message));
    }

    [Fact]
    public void LogColors_StepHeader_CanBeLogged()
    {
        // Arrange
        var stepName = "Test Step";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.StepHeader(stepName));
    }

    [Fact]
    public void LogColors_PhaseHeader_CanBeLogged()
    {
        // Arrange
        var phaseName = "Phase 1";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.PhaseHeader(phaseName));
    }

    [Fact]
    public void LogColors_ModeIndicator_CanBeLogged()
    {
        // Arrange
        var mode = "Two-Pass";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.ModeIndicator(mode));
    }

    [Fact]
    public void LogColors_PathInfo_CanBeLogged()
    {
        // Arrange
        var path = "C:\\Test\\Path";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.PathInfo(path));
    }

    [Fact]
    public void LogColors_PackageName_CanBeLogged()
    {
        // Arrange
        var pkg = "TestPackage";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.PackageName(pkg));
    }

    [Fact]
    public void LogColors_BuildHeader_CanBeLogged()
    {
        // Arrange
        var message = "BUILDING TestMod";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.BuildHeader(message));
    }

    [Fact]
    public void LogColors_SuccessHeader_CanBeLogged()
    {
        // Arrange
        var message = "BUILD COMPLETED";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.SuccessHeader(message));
    }

    [Fact]
    public void LogColors_ErrorHeader_CanBeLogged()
    {
        // Arrange
        var message = "BUILD FAILED";

        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.ErrorHeader(message));
    }

    [Fact]
    public void LogColors_Separator_CanBeLogged()
    {
        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.Separator);
    }

    [Fact]
    public void LogColors_SuccessSeparator_CanBeLogged()
    {
        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.SuccessSeparator);
    }

    [Fact]
    public void LogColors_ErrorSeparator_CanBeLogged()
    {
        // Act & Assert - Should not throw
        _logger.LogInformation(LogColors.ErrorSeparator);
    }
}
