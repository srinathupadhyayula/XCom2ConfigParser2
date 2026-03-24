using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XCom2ModCompiler.Application;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Compilation;
using XCom2ModCompiler.Cooking;
using XCom2ModCompiler.Tracking;
using XCom2ModCompiler.Utilities;

namespace XCom2ModCompiler.Tests;

public class BuildControllerDetailedLoggingTests : IDisposable
{
    private readonly Mock<ILogger<BuildController>> _loggerMock;
    private readonly Mock<BuildTracker> _trackerMock;
    private readonly Mock<ScriptCompiler> _compilerMock;
    private readonly Mock<AssetCooker> _cookerMock;
    private readonly Mock<IFileMirrorParity> _mirrorMock;
    private readonly Mock<IProcessRunner> _processRunnerMock;
    private readonly Mock<ShaderPrecompiler> _shaderPrecompilerMock;
    private readonly Mock<MissingUncookedCopier> _missingUncookedCopierMock;
    private readonly Mock<ProjectSynchronizer> _projectSynchronizerMock;
    private readonly string _tempPath;

    public BuildControllerDetailedLoggingTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        _loggerMock = new Mock<ILogger<BuildController>>();
        var trackerLoggerMock = new Mock<ILogger<BuildTracker>>();
        _trackerMock = new Mock<BuildTracker>(_tempPath, trackerLoggerMock.Object);

        _processRunnerMock = new Mock<IProcessRunner>();
        var compilerLoggerMock = new Mock<ILogger<ScriptCompiler>>();
        _compilerMock = new Mock<ScriptCompiler>("cmd", "sdk", "game", _processRunnerMock.Object, compilerLoggerMock.Object);

        var cookerLoggerMock = new Mock<ILogger<AssetCooker>>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger<ModAssetsCookStep>>().Object);
        _mirrorMock = new Mock<IFileMirrorParity>();
        _cookerMock = new Mock<AssetCooker>("", "", "", _processRunnerMock.Object, _mirrorMock.Object, _trackerMock.Object, loggerFactoryMock.Object, cookerLoggerMock.Object);

        var shaderPrecompilerLoggerMock = new Mock<ILogger<ShaderPrecompiler>>();
        _shaderPrecompilerMock = new Mock<ShaderPrecompiler>(_processRunnerMock.Object, _mirrorMock.Object, shaderPrecompilerLoggerMock.Object);

        var missingUncookedCopierLoggerMock = new Mock<ILogger<MissingUncookedCopier>>();
        _missingUncookedCopierMock = new Mock<MissingUncookedCopier>(_mirrorMock.Object, missingUncookedCopierLoggerMock.Object);

        var projectSynchronizerLoggerMock = new Mock<ILogger<ProjectSynchronizer>>();
        _projectSynchronizerMock = new Mock<ProjectSynchronizer>(projectSynchronizerLoggerMock.Object);
    }

    [Fact]
    public async Task InvokeBuildAsync_LogsModCopyProgress()
    {
        // Arrange
        var options = CreateValidOptions();
        var controller = CreateController(options);

        _compilerMock.Setup(x => x.CompileBaseAsync(It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _compilerMock.Setup(x => x.CompileModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await controller.InvokeBuildAsync();

        // Assert - verify "Mirroring" and "SDK" are logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Mirroring") && o.ToString()!.Contains("SDK")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeBuildAsync_LogsLocalizationConversion()
    {
        // Arrange
        var options = CreateValidOptions();
        var controller = CreateController(options);

        _compilerMock.Setup(x => x.CompileBaseAsync(It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _compilerMock.Setup(x => x.CompileModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await controller.InvokeBuildAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Converting") && o.ToString()!.Contains("localization")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeBuildAsync_LogsShaderPrecompilation()
    {
        // Arrange
        var options = CreateValidOptions();
        var controller = CreateController(options);

        _compilerMock.Setup(x => x.CompileBaseAsync(It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _compilerMock.Setup(x => x.CompileModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await controller.InvokeBuildAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Precompiling") && o.ToString()!.Contains("shader")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeBuildAsync_LogsScriptPackageCopying()
    {
        // Arrange - create Src folder to trigger script package logic
        var options = CreateValidOptions();
        var srcPath = Path.Combine(options.ModSrcRoot, "Src");
        Directory.CreateDirectory(srcPath);
        Directory.CreateDirectory(Path.Combine(srcPath, "TestPackage"));
        
        var controller = CreateController(options);

        _compilerMock.Setup(x => x.CompileBaseAsync(It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _compilerMock.Setup(x => x.CompileModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await controller.InvokeBuildAsync();

        // Assert - verify script package copying is logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Copying") && o.ToString()!.Contains("script packages")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private BuildOptions CreateValidOptions()
    {
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = _tempPath,
            SdkPath = Path.Combine(_tempPath, "SDK"),
            GamePath = Path.Combine(_tempPath, "Game"),
            ModDestinationPath = Path.Combine(_tempPath, "Dest")
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
        return new BuildController(
            options,
            _loggerMock.Object,
            _trackerMock.Object,
            _compilerMock.Object,
            _cookerMock.Object,
            _mirrorMock.Object,
            _processRunnerMock.Object,
            _shaderPrecompilerMock.Object,
            _missingUncookedCopierMock.Object,
            _projectSynchronizerMock.Object
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }
}
