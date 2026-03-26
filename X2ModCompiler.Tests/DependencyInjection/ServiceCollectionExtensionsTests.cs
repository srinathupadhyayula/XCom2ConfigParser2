using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using X2ModCompiler.Application;
using X2ModCompiler.Configuration;
using X2ModCompiler.Cooking;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Compilation;
using X2ModCompiler.DependencyInjection;

namespace X2ModCompiler.Tests.DependencyInjection;

/// <summary>
/// Tests for the dependency injection service collection extensions.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddX2ModCompiler_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = ".",
            SdkPath = ".",
            GamePath = ".",
            ModDestinationPath = "."
        };

        // Act
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - All core services should be resolvable
        Assert.NotNull(serviceProvider.GetService<BuildOptions>());
        Assert.NotNull(serviceProvider.GetService<ILoggerFactory>());
        Assert.NotNull(serviceProvider.GetService<IProcessRunner>());
        Assert.NotNull(serviceProvider.GetService<IFileMirrorParity>());
        Assert.NotNull(serviceProvider.GetService<BuildTracker>());
        Assert.NotNull(serviceProvider.GetService<ScriptCompiler>());
        Assert.NotNull(serviceProvider.GetService<AssetCooker>());
        Assert.NotNull(serviceProvider.GetService<ShaderPrecompiler>());
        Assert.NotNull(serviceProvider.GetService<MissingUncookedCopier>());
        Assert.NotNull(serviceProvider.GetService<ProjectSynchronizer>());
        Assert.NotNull(serviceProvider.GetService<FileProcessor>());
        Assert.NotNull(serviceProvider.GetService<ScriptCleaner>());
        Assert.NotNull(serviceProvider.GetService<BuildController>());
    }

    [Fact]
    public void AddX2ModCompiler_OptionsAreRegisteredAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = ".",
            SdkPath = ".",
            GamePath = ".",
            ModDestinationPath = "."
        };

        // Act
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options1 = serviceProvider.GetRequiredService<BuildOptions>();
        var options2 = serviceProvider.GetRequiredService<BuildOptions>();
        Assert.Same(options1, options2); // Should be same instance (singleton)
        Assert.Same(options, options1); // Should be the original instance
    }

    [Fact]
    public void AddX2ModCompiler_LoggerFactoryIsRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = ".",
            SdkPath = ".",
            GamePath = ".",
            ModDestinationPath = ".",
            Debug = true
        };

        // Act
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        Assert.NotNull(loggerFactory);
        
        // Verify logger can be created
        var logger = loggerFactory.CreateLogger<BuildController>();
        Assert.NotNull(logger);
    }

    [Fact]
    public void AddX2ModCompiler_BuildControllerIsResolvable()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = ".",
            SdkPath = ".",
            GamePath = ".",
            ModDestinationPath = "."
        };

        // Act
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var controller = serviceProvider.GetRequiredService<BuildController>();
        Assert.NotNull(controller);
    }

    [Fact]
    public void AddX2ModCompiler_ServicesHaveCorrectLifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = ".",
            SdkPath = ".",
            GamePath = ".",
            ModDestinationPath = "."
        };

        // Act
        services.AddX2ModCompiler(options);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - All services should be singletons
        var runner1 = serviceProvider.GetRequiredService<IProcessRunner>();
        var runner2 = serviceProvider.GetRequiredService<IProcessRunner>();
        Assert.Same(runner1, runner2);

        var mirror1 = serviceProvider.GetRequiredService<IFileMirrorParity>();
        var mirror2 = serviceProvider.GetRequiredService<IFileMirrorParity>();
        Assert.Same(mirror1, mirror2);

        var compiler1 = serviceProvider.GetRequiredService<ScriptCompiler>();
        var compiler2 = serviceProvider.GetRequiredService<ScriptCompiler>();
        Assert.Same(compiler1, compiler2);
    }
}
