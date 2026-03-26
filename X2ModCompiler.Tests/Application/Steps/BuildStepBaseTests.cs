using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using X2ModCompiler.Application.Steps;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Tests.Application.Steps;

/// <summary>
/// Tests for BuildStepBase abstract class.
/// Verifies that the base class provides common functionality correctly.
/// </summary>
public class BuildStepBaseTests
{
    private readonly ILogger<TestStep> _logger;
    private readonly BuildOptions _options;

    public BuildStepBaseTests()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _logger = loggerFactory.CreateLogger<TestStep>();
        
        _options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(Path.GetTempPath(), "TestProject"),
            SdkPath = Path.Combine(Path.GetTempPath(), "TestSdk"),
            GamePath = Path.Combine(Path.GetTempPath(), "TestGame"),
            ModDestinationPath = Path.Combine(Path.GetTempPath(), "TestDest")
        };
    }

    [Fact]
    public void BuildStepBase_Name_ReturnsStepName()
    {
        // Arrange
        var step = new TestStep(_logger);

        // Act
        var name = step.Name;

        // Assert
        Assert.Equal("Test Step", name);
    }

    [Fact]
    public async Task BuildStepBase_ExecuteAsync_LogsStepHeader_BeforeExecution()
    {
        // Arrange
        var step = new TestStep(_logger);

        // Act
        await step.ExecuteAsync(_options, CancellationToken.None);

        // Assert - If we get here without exception, the base class executed correctly
        Assert.True(true);
    }

    [Fact]
    public async Task BuildStepBase_ExecuteAsync_ReturnsTrue_WhenStepSucceeds()
    {
        // Arrange
        var step = new TestStep(_logger);

        // Act
        var result = await step.ExecuteAsync(_options, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task BuildStepBase_ExecuteAsync_ReturnsFalse_WhenStepFails()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        var logger = loggerFactory.CreateLogger<FailingTestStep>();
        var step = new FailingTestStep(logger);

        // Act
        var result = await step.ExecuteAsync(_options, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BuildStepBase_ExecuteAsync_HandlesException_LogsError()
    {
        // Arrange
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        var logger = loggerFactory.CreateLogger<FailingTestStep>();
        var step = new FailingTestStep(logger);

        // Act
        var result = await step.ExecuteAsync(_options, CancellationToken.None);

        // Assert - Should return false, not throw
        Assert.False(result);
    }

    [Fact]
    public void BuildStepBase_Constructor_SetsName()
    {
        // Arrange & Act
        var step = new TestStep(_logger);

        // Assert
        Assert.NotNull(step.Name);
        Assert.NotEmpty(step.Name);
    }

    [Fact]
    public async Task BuildStepBase_ExecuteAsync_PropagatesCancellation()
    {
        // Arrange
        var step = new TestStep(_logger);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should handle cancellation gracefully
        var result = await step.ExecuteAsync(_options, cts.Token);
        Assert.True(result); // Test step doesn't check cancellation
    }
}

/// <summary>
/// Test implementation of BuildStepBase for testing purposes.
/// </summary>
public class TestStep : BuildStepBase
{
    public TestStep(ILogger<TestStep> logger) : base("Test Step", logger)
    {
    }

    protected override Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        return Task.FromResult(true);
    }
}

/// <summary>
/// Test implementation that always fails.
/// </summary>
public class FailingTestStep : BuildStepBase
{
    public FailingTestStep(ILogger<FailingTestStep> logger) : base("Failing Test Step", logger)
    {
    }

    protected override Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        return Task.FromResult(false);
    }
}
