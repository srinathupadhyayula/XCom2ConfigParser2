using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using X2ModCompiler.Application.Steps;
using X2ModCompiler.Application;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Tests.Application.Steps;

/// <summary>
/// Tests verifying that build steps correctly inherit from BuildStepBase.
/// </summary>
public class BuildStepInheritanceTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly BuildOptions _options;

    public BuildStepInheritanceTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        
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
    public void ScriptCleanupStep_ImplementsIBuildStep()
    {
        // Arrange & Act
        var step = new ScriptCleanupStep(_loggerFactory.CreateLogger<ScriptCleanupStep>());

        // Assert
        Assert.IsAssignableFrom<IBuildStep>(step);
        Assert.NotNull(step.Name);
        Assert.NotEmpty(step.Name);
    }

    [Fact]
    public async Task ScriptCleanupStep_ExecuteAsync_ReturnsTrue_OnSuccess()
    {
        // Arrange
        var step = new ScriptCleanupStep(_loggerFactory.CreateLogger<ScriptCleanupStep>());

        // Act
        var result = await step.ExecuteAsync(_options, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void FinalCopyStep_ImplementsIBuildStep()
    {
        // Arrange & Act
        var step = new FinalCopyStep(_loggerFactory.CreateLogger<FinalCopyStep>());

        // Assert
        Assert.IsAssignableFrom<IBuildStep>(step);
        Assert.NotNull(step.Name);
        Assert.NotEmpty(step.Name);
    }

    [Fact]
    public async Task FinalCopyStep_ExecuteAsync_HandlesMissingStaging_Gracefully()
    {
        // Arrange
        var step = new FinalCopyStep(_loggerFactory.CreateLogger<FinalCopyStep>());

        // Act - Don't create staging directory, test should handle missing source
        var result = await step.ExecuteAsync(_options, CancellationToken.None);

        // Assert - Should handle gracefully (return false, not crash)
        Assert.False(result); // Expected to fail when staging doesn't exist
    }

    [Fact]
    public async Task BuildStepBase_ProvidesCommonErrorHandling()
    {
        // Arrange
        var testStep = new TestErrorStep(_loggerFactory.CreateLogger<TestErrorStep>());

        // Act
        var exception = await Record.ExceptionAsync(async () => await testStep.ExecuteAsync(_options, CancellationToken.None));

        // Assert - Should not throw, should return false
        Assert.Null(exception);
    }

    [Fact]
    public async Task BuildStepBase_ProvidesCommonCancellationHandling()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var testStep = new TestCancellationStep(_loggerFactory.CreateLogger<TestCancellationStep>());

        // Act
        var exception = await Record.ExceptionAsync(async () => await testStep.ExecuteAsync(_options, cts.Token));

        // Assert - Should handle cancellation gracefully
        Assert.Null(exception);
    }
}

/// <summary>
/// Test step that throws an exception to verify error handling.
/// </summary>
public class TestErrorStep : BuildStepBase
{
    public TestErrorStep(ILogger<TestErrorStep> logger) : base("Test Error Step", logger) { }

    protected override Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        throw new InvalidOperationException("Test exception");
    }
}

/// <summary>
/// Test step that checks cancellation token.
/// </summary>
public class TestCancellationStep : BuildStepBase
{
    public TestCancellationStep(ILogger<TestCancellationStep> logger) : base("Test Cancellation Step", logger) { }

    protected override Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
