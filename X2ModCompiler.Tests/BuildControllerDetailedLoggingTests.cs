using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Application;
using X2ModCompiler.Configuration;
using X2ModCompiler.Compilation;
using X2ModCompiler.Cooking;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;
using X2ModCompiler.Tests.Shared;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Core.Configuration;

namespace X2ModCompiler.Tests;

public class BuildControllerDetailedLoggingTests : TestBase
{
    private readonly ILogger<BuildController> _logger;
    private readonly BuildTracker _tracker;
    private readonly ScriptCompiler _compiler;
    private readonly AssetCooker _cooker;
    private readonly IFileMirrorParity _mirror;
    private readonly IProcessRunner _processRunner;
    private readonly ShaderPrecompiler _shaderPrecompiler;
    private readonly MissingUncookedCopier _missingUncookedCopier;
    private readonly ProjectSynchronizer _projectSynchronizer;
    private readonly ScriptCleaner _scriptCleaner;

    public BuildControllerDetailedLoggingTests()
    {
        _logger = Substitute.For<ILogger<BuildController>>();
        _tracker = Substitute.For<BuildTracker>("tempPath", Substitute.For<ILogger<BuildTracker>>());

        _processRunner = Substitute.For<IProcessRunner>();
        _compiler = Substitute.For<ScriptCompiler>("cmd", "sdk", "game", _processRunner, Substitute.For<ILogger<ScriptCompiler>>());

        _mirror = Substitute.For<IFileMirrorParity>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger<ModAssetsCookStep>>());
        _cooker = Substitute.For<AssetCooker>("", "", "", _processRunner, _mirror, _tracker, loggerFactory, Substitute.For<ILogger<AssetCooker>>());

        var lcf = Substitute.For<ILoggerFactory>();
        _shaderPrecompiler = Substitute.For<ShaderPrecompiler>(_processRunner, _mirror, lcf, Substitute.For<ILogger<ShaderPrecompiler>>());
        _missingUncookedCopier = Substitute.For<MissingUncookedCopier>(_mirror, Substitute.For<ILogger<MissingUncookedCopier>>());
        _projectSynchronizer = Substitute.For<ProjectSynchronizer>(Substitute.For<ILogger<ProjectSynchronizer>>());
        _scriptCleaner = Substitute.For<ScriptCleaner>(Substitute.For<ILogger<ScriptCleaner>>());
    }

    [Fact]
    public async Task InvokeBuildAsync_LogsModCopyProgress()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var options = CreateValidOptions(temp.Path);
        var controller = CreateController(options);

        _compiler.CompileBaseAsync(Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _compiler.CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        // Assert - verify "Mirroring" and "SDK" are logged
        _logger.ReceivedWithAnyArgs().Log<object>(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>((o) => o != null && o.ToString()!.Contains("Mirroring") && o.ToString()!.Contains("SDK")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeBuildAsync_LogsLocalizationConversion()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var options = CreateValidOptions(temp.Path);
        var controller = CreateController(options);

        _compiler.CompileBaseAsync(Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _compiler.CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.ReceivedWithAnyArgs().Log<object>(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>((o) => o != null && o.ToString()!.Contains("Converting") && o.ToString()!.Contains("localization")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeBuildAsync_LogsShaderPrecompilation()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var options = CreateValidOptions(temp.Path);
        var controller = CreateController(options);

        _compiler.CompileBaseAsync(Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _compiler.CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.ReceivedWithAnyArgs().Log<object>(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>((o) => o != null && o.ToString()!.Contains("Precompiling") && o.ToString()!.Contains("shader")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task InvokeBuildAsync_LogsScriptPackageCopying()
    {
        await using var temp = new TempDirectory();
        // Arrange - create Src folder to trigger script package logic
        var options = CreateValidOptions(temp.Path);
        var srcPath = Path.Combine(options.ModSrcRoot, "Src");
        Directory.CreateDirectory(srcPath);
        Directory.CreateDirectory(Path.Combine(srcPath, "TestPackage"));
        
        var controller = CreateController(options);

        _compiler.CompileBaseAsync(Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _compiler.CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        // Assert - verify script package copying is logged
        _logger.ReceivedWithAnyArgs().Log<object>(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>((o) => o != null && o.ToString()!.Contains("Copying") && o.ToString()!.Contains("script packages")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    private BuildOptions CreateValidOptions(string tempPath)
    {
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = tempPath,
            SdkPath = Path.Combine(tempPath, "SDK"),
            GamePath = Path.Combine(tempPath, "Game"),
            ModDestinationPath = Path.Combine(tempPath, "Dest")
        };

        Directory.CreateDirectory(options.SdkPath);
        Directory.CreateDirectory(options.GamePath);
        Directory.CreateDirectory(options.ProjectRoot);
        Directory.CreateDirectory(options.ModSrcRoot);
        File.WriteAllText(Path.Combine(options.ProjectRoot, options.ModName + ".x2proj"), "<Project></Project>");

        return options;
    }

    private BuildController CreateController(BuildOptions options)
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<BuildController>().Returns(_logger);

        var services = new BuildServices(
            _tracker,
            _compiler,
            _cooker,
            _mirror,
            _processRunner,
            _shaderPrecompiler,
            _missingUncookedCopier,
            _projectSynchronizer,
            new FileProcessor(Substitute.For<IValidator>(), new ParserSettings(), false, loggerFactory),
            _scriptCleaner
        );
        return new BuildController(options, loggerFactory, services);
    }
}
