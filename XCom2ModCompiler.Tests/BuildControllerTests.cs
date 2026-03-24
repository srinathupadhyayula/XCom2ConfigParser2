using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XCom2ModCompiler.Application;
using XCom2ModCompiler.Compilation;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Cooking;
using XCom2ModCompiler.Tracking;
using XCom2ModCompiler.Utilities;
using System.Linq;
using System.Collections.Generic;

namespace XCom2ModCompiler.Tests;

public class BuildControllerTests
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

    public BuildControllerTests()
    {
        _loggerMock = new Mock<ILogger<BuildController>>();
        
        var trackerLoggerMock = new Mock<ILogger<BuildTracker>>();
        _trackerMock = new Mock<BuildTracker>("cachePath", trackerLoggerMock.Object);

        _processRunnerMock = new Mock<IProcessRunner>();
        var compilerLoggerMock = new Mock<ILogger<ScriptCompiler>>();
        _compilerMock = new Mock<ScriptCompiler>("cmd", "sdk", "game", _processRunnerMock.Object, compilerLoggerMock.Object);

        var cookerLoggerMock = new Mock<ILogger<AssetCooker>>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        _mirrorMock = new Mock<IFileMirrorParity>();
        _cookerMock = new Mock<AssetCooker>("", "", "", _processRunnerMock.Object, _mirrorMock.Object, _trackerMock.Object, loggerFactoryMock.Object, cookerLoggerMock.Object);

        var shaderPrecompilerLoggerMock = new Mock<ILogger<ShaderPrecompiler>>();
        _shaderPrecompilerMock = new Mock<ShaderPrecompiler>(_processRunnerMock.Object, _mirrorMock.Object, shaderPrecompilerLoggerMock.Object);

        var missingUncookedCopierLoggerMock = new Mock<ILogger<MissingUncookedCopier>>();
        _missingUncookedCopierMock = new Mock<MissingUncookedCopier>(_mirrorMock.Object, missingUncookedCopierLoggerMock.Object);

        var projectSynchronizerLoggerMock = new Mock<ILogger<ProjectSynchronizer>>();
        _projectSynchronizerMock = new Mock<ProjectSynchronizer>(projectSynchronizerLoggerMock.Object);
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

    private BuildOptions CreateValidOptions(string basePath)
    {
        return new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(basePath, "Src", "TestMod"),
            SdkPath = Path.Combine(basePath, "SDK"),
            GamePath = Path.Combine(basePath, "Game"),
            ModDestinationPath = Path.Combine(basePath, "Dest", "TestMod")
        };
    }

    [Fact]
    public async Task InvokeBuildAsync_ReturnsFalse_WhenConfigurationIsInvalid()
    {
        var options = new BuildOptions(); // Missing required properties
        var controller = CreateController(options);

        var result = await controller.InvokeBuildAsync();

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("SdkPath"));
    }

    [Fact]
    public async Task InvokeBuildAsync_ExecutesFullPipeline_WhenSuccessful()
    {
        string testBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = CreateValidOptions(testBase);
        
        // Create dummy directories to bypass validation
        Directory.CreateDirectory(options.SdkPath);
        Directory.CreateDirectory(options.GamePath);
        Directory.CreateDirectory(options.ProjectRoot);
        Directory.CreateDirectory(options.ModSrcRoot);
        File.WriteAllText(Path.Combine(options.ProjectRoot, options.ModName + ".x2proj"), "<Project></Project>");

        try
        {
            var controller = CreateController(options);

            _compilerMock.Setup(c => c.CompileBaseAsync(It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(true);
            _compilerMock.Setup(c => c.CompileModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync(true);

            var result = await controller.InvokeBuildAsync();

            if (!result.Success)
            {
                foreach (var err in result.Errors) _loggerMock.Object.LogError(err);
            }

            Assert.True(result.Success);

            // Verify Mirror operations (Src to SDK, Staging to Final)
            _mirrorMock.Verify(m => m.MirrorAsync(
                options.ModSrcRoot, 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string[]?>(), 
                It.IsAny<string[]?>(), 
                It.IsAny<CancellationToken>()), Times.Once);
            _mirrorMock.Verify(m => m.MirrorAsync(
                options.StagingPath, 
                options.FinalModPath, 
                It.IsAny<string>(), 
                It.IsAny<string[]?>(), 
                It.IsAny<string[]?>(), 
                It.IsAny<CancellationToken>()), Times.Once);

            // Verify compilation
            _compilerMock.Verify(c => c.CompileBaseAsync(options, It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()), Times.Once);
            _compilerMock.Verify(c => c.CompileModAsync("TestMod", options.StagingPath, options, It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()), Times.Once);

            // Verify fingerprint saved
            _trackerMock.Verify(t => t.SaveFingerprintAsync(It.IsAny<BuildFingerprint>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (Directory.Exists(testBase)) Directory.Delete(testBase, true);
        }
    }

    [Fact]
    public async Task InvokeBuildAsync_InvokesCooker_WhenContentOptionsArePresent()
    {
        string testBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = CreateValidOptions(testBase);
        options.ContentOptionsJson = "options.json"; // This triggers cooking
        
        Directory.CreateDirectory(options.SdkPath);
        Directory.CreateDirectory(options.GamePath);
        Directory.CreateDirectory(options.ProjectRoot);
        Directory.CreateDirectory(options.ModSrcRoot);
        File.WriteAllText(Path.Combine(options.ProjectRoot, options.ModName + ".x2proj"), "<Project></Project>");
        File.WriteAllText(Path.Combine(options.ModSrcRoot, "options.json"), "{}");

        try
        {
            var controller = CreateController(options);

            _compilerMock.Setup(c => c.CompileBaseAsync(It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _compilerMock.Setup(c => c.CompileModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _cookerMock.Setup(c => c.CookAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ContentOptions>(), It.IsAny<BuildOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await controller.InvokeBuildAsync();

            if (!result.Success)
            {
                foreach (var err in result.Errors) _loggerMock.Object.LogError(err);
            }

            Assert.True(result.Success);
            _cookerMock.Verify(c => c.CookAsync("TestMod", options.StagingPath, It.IsAny<ContentOptions>(), options, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (Directory.Exists(testBase)) Directory.Delete(testBase, true);
        }
    }

    [Fact]
    public async Task InvokeCleanAsync_DeletesExpectedDirectories()
    {
        string testBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var options = CreateValidOptions(testBase);
        var controller = CreateController(options);

        Directory.CreateDirectory(options.BuildCachePath);
        var result = await controller.InvokeCleanAsync();

        Assert.True(result.Success);
        _mirrorMock.Verify(m => m.DeleteAsync(It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
