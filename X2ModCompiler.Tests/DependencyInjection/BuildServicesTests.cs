using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Application;
using X2ModCompiler.Compilation;
using X2ModCompiler.Cooking;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;
using X2ModCompiler.Core.Validation;

namespace X2ModCompiler.Tests.DependencyInjection;

/// <summary>
/// Tests for the BuildServices facade class.
/// </summary>
public class BuildServicesTests
{
    [Fact]
    public void BuildServices_Constructor_StoresAllServices()
    {
        // Arrange - Use mocks for interfaces, real instances for sealed classes
        var tracker = Substitute.For<BuildTracker>("path", Substitute.For<ILogger<BuildTracker>>());
        var compiler = Substitute.For<ScriptCompiler>("cmd", "sdk", "game", Substitute.For<IProcessRunner>(), Substitute.For<ILogger<ScriptCompiler>>());
        var cooker = Substitute.For<AssetCooker>("sdk", "game", "cache", Substitute.For<IProcessRunner>(), Substitute.For<IFileMirrorParity>(), tracker, Substitute.For<ILoggerFactory>(), Substitute.For<ILogger<AssetCooker>>());
        var mirror = Substitute.For<IFileMirrorParity>();
        var processRunner = Substitute.For<IProcessRunner>();
        var shaderPrecompiler = Substitute.For<ShaderPrecompiler>(processRunner, mirror, Substitute.For<ILoggerFactory>(), Substitute.For<ILogger<ShaderPrecompiler>>());
        var missingUncookedCopier = Substitute.For<MissingUncookedCopier>(mirror, Substitute.For<ILogger<MissingUncookedCopier>>());
        var projectSynchronizer = Substitute.For<ProjectSynchronizer>(Substitute.For<ILogger<ProjectSynchronizer>>());
        var fileProcessor = new FileProcessor(Substitute.For<IValidator>(), new Core.Configuration.ParserSettings(), false, Substitute.For<ILoggerFactory>());
        var scriptCleaner = Substitute.For<ScriptCleaner>(Substitute.For<ILogger<ScriptCleaner>>());

        // Act
        var services = new BuildServices(
            tracker,
            compiler,
            cooker,
            mirror,
            processRunner,
            shaderPrecompiler,
            missingUncookedCopier,
            projectSynchronizer,
            fileProcessor,
            scriptCleaner);

        // Assert - All properties should return the injected instances
        Assert.Same(tracker, services.Tracker);
        Assert.Same(compiler, services.Compiler);
        Assert.Same(cooker, services.Cooker);
        Assert.Same(mirror, services.Mirror);
        Assert.Same(processRunner, services.ProcessRunner);
        Assert.Same(shaderPrecompiler, services.ShaderPrecompiler);
        Assert.Same(missingUncookedCopier, services.MissingUncookedCopier);
        Assert.Same(projectSynchronizer, services.ProjectSynchronizer);
        Assert.Same(fileProcessor, services.FileProcessor);
        Assert.Same(scriptCleaner, services.ScriptCleaner);
    }

    [Fact]
    public void BuildServices_PropertiesAreNotNull()
    {
        // Arrange
        var tracker = Substitute.For<BuildTracker>("path", Substitute.For<ILogger<BuildTracker>>());
        var compiler = Substitute.For<ScriptCompiler>("cmd", "sdk", "game", Substitute.For<IProcessRunner>(), Substitute.For<ILogger<ScriptCompiler>>());
        var cooker = Substitute.For<AssetCooker>("sdk", "game", "cache", Substitute.For<IProcessRunner>(), Substitute.For<IFileMirrorParity>(), tracker, Substitute.For<ILoggerFactory>(), Substitute.For<ILogger<AssetCooker>>());
        var mirror = Substitute.For<IFileMirrorParity>();
        var processRunner = Substitute.For<IProcessRunner>();
        var shaderPrecompiler = Substitute.For<ShaderPrecompiler>(processRunner, mirror, Substitute.For<ILoggerFactory>(), Substitute.For<ILogger<ShaderPrecompiler>>());
        var missingUncookedCopier = Substitute.For<MissingUncookedCopier>(mirror, Substitute.For<ILogger<MissingUncookedCopier>>());
        var projectSynchronizer = Substitute.For<ProjectSynchronizer>(Substitute.For<ILogger<ProjectSynchronizer>>());
        var fileProcessor = new FileProcessor(Substitute.For<IValidator>(), new Core.Configuration.ParserSettings(), false, Substitute.For<ILoggerFactory>());
        var scriptCleaner = Substitute.For<ScriptCleaner>(Substitute.For<ILogger<ScriptCleaner>>());

        // Act
        var services = new BuildServices(
            tracker,
            compiler,
            cooker,
            mirror,
            processRunner,
            shaderPrecompiler,
            missingUncookedCopier,
            projectSynchronizer,
            fileProcessor,
            scriptCleaner);

        // Assert - All properties should be non-null
        Assert.NotNull(services.Tracker);
        Assert.NotNull(services.Compiler);
        Assert.NotNull(services.Cooker);
        Assert.NotNull(services.Mirror);
        Assert.NotNull(services.ProcessRunner);
        Assert.NotNull(services.ShaderPrecompiler);
        Assert.NotNull(services.MissingUncookedCopier);
        Assert.NotNull(services.ProjectSynchronizer);
        Assert.NotNull(services.FileProcessor);
        Assert.NotNull(services.ScriptCleaner);
    }
}
