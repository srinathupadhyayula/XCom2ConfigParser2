using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using X2ModCompiler.Application;
using X2ModCompiler.Configuration;
using X2ModCompiler.DependencyInjection;

namespace X2ModCompiler.Tests.DependencyInjection;

/// <summary>
/// Integration tests for Program.cs DI container setup.
/// </summary>
public class ProgramDiIntegrationTests
{
    [Fact]
    public void BuildCommand_CanResolveController_FromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = CreateValidOptions();
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var controller = serviceProvider.GetRequiredService<BuildController>();

        // Assert
        Assert.NotNull(controller);
    }

    [Fact]
    public void BuildCommand_AllServicesAreRegistered_ForBuildPipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = CreateValidOptions();
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - All required services should be resolvable
        Assert.NotNull(serviceProvider.GetRequiredService<BuildOptions>());
        Assert.NotNull(serviceProvider.GetRequiredService<ILoggerFactory>());
        Assert.NotNull(serviceProvider.GetRequiredService<BuildController>());
        Assert.NotNull(serviceProvider.GetRequiredService<BuildServices>());
    }

    [Fact]
    public void BuildCommand_OptionsArePassedCorrectly_ToServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = "/test/project",
            SdkPath = "/test/sdk",
            GamePath = "/test/game",
            ModDestinationPath = "/test/dest",
            Debug = true,
            LogVerbosity = Core.Configuration.CompilerLogLevel.Debug
        };
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var resolvedOptions = serviceProvider.GetRequiredService<BuildOptions>();

        // Assert
        Assert.Equal("TestMod", resolvedOptions.ModName);
        Assert.Equal("/test/project", resolvedOptions.ProjectRoot);
        Assert.True(resolvedOptions.Debug);
    }

    [Fact]
    public void BuildCommand_LoggerFactoryIsConfigured_Correctly()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = CreateValidOptions();
        options.Debug = true;
        options.LogVerbosity = Core.Configuration.CompilerLogLevel.Debug;
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<BuildController>();

        // Assert
        Assert.NotNull(logger);
        Assert.NotNull(loggerFactory);
    }

    [Fact]
    public void BuildCommand_ServicesAreSingletons_AsExpected()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = CreateValidOptions();
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var controller1 = serviceProvider.GetRequiredService<BuildController>();
        var controller2 = serviceProvider.GetRequiredService<BuildController>();

        // Assert
        Assert.Same(controller1, controller2); // Should be same instance
    }

    private static BuildOptions CreateValidOptions()
    {
        return new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = ".",
            SdkPath = ".",
            GamePath = ".",
            ModDestinationPath = ".",
            Debug = false,
            LogVerbosity = Core.Configuration.CompilerLogLevel.Information
        };
    }
}
