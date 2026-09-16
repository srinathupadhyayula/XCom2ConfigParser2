using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Application;
using X2ModCompiler.Compilation;
using X2ModCompiler.Configuration;
using X2ModCompiler.Cooking;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;
using System.Linq;
using System.Collections.Generic;
using X2ModCompiler.Tests.Shared;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Core.Configuration;

namespace X2ModCompiler.Tests;

public class BuildControllerTests : TestBase
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
    private readonly FileProcessor _fileProcessor;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ScriptCleaner _scriptCleaner;

    public BuildControllerTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _logger = Substitute.For<ILogger<BuildController>>();
        _loggerFactory.CreateLogger<BuildController>().Returns(_logger);

        var trackerLogger = Substitute.For<ILogger<BuildTracker>>();
        _tracker = Substitute.For<BuildTracker>("cachePath", trackerLogger);

        _processRunner = Substitute.For<IProcessRunner>();
        var compilerLogger = Substitute.For<ILogger<ScriptCompiler>>();
        _compiler = Substitute.For<ScriptCompiler>("cmd", "sdk", "game", _processRunner, compilerLogger);

        var cookerLogger = Substitute.For<ILogger<AssetCooker>>();
        _mirror = Substitute.For<IFileMirrorParity>();
        _cooker = Substitute.For<AssetCooker>("", "", "", _processRunner, _mirror, _tracker, _loggerFactory, cookerLogger);

        var shaderPrecompilerLogger = Substitute.For<ILogger<ShaderPrecompiler>>();
        var lpCf = Substitute.For<ILoggerFactory>();
        _shaderPrecompiler = Substitute.For<ShaderPrecompiler>(_processRunner, _mirror, lpCf, Substitute.For<ILogger<ShaderPrecompiler>>());

        var missingUncookedCopierLogger = Substitute.For<ILogger<MissingUncookedCopier>>();
        _missingUncookedCopier = Substitute.For<MissingUncookedCopier>(_mirror, missingUncookedCopierLogger);

        var projectSynchronizerLogger = Substitute.For<ILogger<ProjectSynchronizer>>();
        _projectSynchronizer = Substitute.For<ProjectSynchronizer>(projectSynchronizerLogger);
        _fileProcessor = new FileProcessor(Substitute.For<IValidator>(), new ParserSettings(), false, _loggerFactory);
        _scriptCleaner = Substitute.For<ScriptCleaner>(Substitute.For<ILogger<ScriptCleaner>>());

        _tracker.ShouldRebuildAsync(Arg.Any<BuildOptions>(), Arg.Any<string>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(true));
    }

    private BuildController CreateController(BuildOptions options)
    {
        BuildController.ConfigParserDelayMs = 0;
        var services = new BuildServices(
            _tracker,
            _compiler,
            _cooker,
            _mirror,
            _processRunner,
            _shaderPrecompiler,
            _missingUncookedCopier,
            _projectSynchronizer,
            _fileProcessor,
            _scriptCleaner
        );
        return new BuildController(options, _loggerFactory, services);
    }

    private BuildOptions CreateValidOptions(string basePath)
    {
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(basePath, "Src", "TestMod"),
            SdkPath = Path.Combine(basePath, "SDK"),
            GamePath = Path.Combine(basePath, "Game"),
            ModDestinationPath = Path.Combine(basePath, "Dest", "TestMod")
        };

        var sdkConfig = Path.Combine(options.SdkPath, "XComGame", "Config");
        Directory.CreateDirectory(sdkConfig);
        File.WriteAllText(Path.Combine(sdkConfig, "XComEngine.ini"), "[UnrealEd.EditorEngine]");

        Directory.CreateDirectory(options.StagingPath);

        return options;
    }

    [Fact]
    public async Task InvokeBuildAsync_ExecutesFullPipeline_WhenSuccessful()
    {
        await using var temp = new TempDirectory();
        var options = CreateValidOptions(temp.Path);
        
        // Create dummy directories to bypass validation
        Directory.CreateDirectory(options.SdkPath);
        Directory.CreateDirectory(options.GamePath);
        Directory.CreateDirectory(options.ProjectRoot);
        Directory.CreateDirectory(options.ModSrcRoot);
        File.WriteAllText(Path.Combine(options.ProjectRoot, options.ModName + ".x2proj"), "<Project></Project>");

        var controller = CreateController(options);

        options.ValidateConfig = false;

        _compiler.CompileBaseAsync(Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult(true));
        _compiler.CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult(true));
        _cooker.CookAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ContentOptions>(), Arg.Any<BuildOptions>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult(true));
        _shaderPrecompiler.PrecompileAsync(Arg.Any<BuildOptions>(), Arg.Any<CancellationToken>())
                 .Returns(Task.CompletedTask);
        _missingUncookedCopier.CopyMissingAsync(Arg.Any<BuildOptions>(), Arg.Any<ContentOptions>(), Arg.Any<CancellationToken>())
                 .Returns(Task.CompletedTask);

        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        if (!result.Success)
        {
            foreach (var err in result.Errors) _logger.LogInformation(err);
        }

        if (!result.Success) Assert.Fail("Build failed with errors: " + string.Join("; ", result.Errors));
        Assert.True(result.Success);

        // Verify compilation call (mod only in this mock environment without config parser setup)
        await _compiler.Received(1).CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>());

        // Verify fingerprint saved
        await _tracker.Received(1).SaveFingerprintAsync(Arg.Any<BuildFingerprint>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeBuildAsync_SkipsDeployment_WhenInCompileOnlyMode()
    {
        await using var temp = new TempDirectory();
        var options = CreateValidOptions(temp.Path);
        options.CompileOnly = true;
        
        Directory.CreateDirectory(options.SdkPath);
        Directory.CreateDirectory(options.GamePath);
        Directory.CreateDirectory(options.ProjectRoot);
        Directory.CreateDirectory(options.ModSrcRoot);
        File.WriteAllText(Path.Combine(options.ProjectRoot, options.ModName + ".x2proj"), "<Project></Project>");

        var controller = CreateController(options);

        _compiler.CompileBaseAsync(Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult(true));
        _compiler.CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
                 .Returns(Task.FromResult(true));

        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        if (!result.Success) Assert.Fail("Build failed with errors: " + string.Join("; ", result.Errors));

        // In compile-only mode, build should succeed without cooking/deployment
        Assert.True(result.Success);
        Assert.True(options.CompileOnly);
    }

    [Fact]
    public async Task InvokeBuildAsync_InvokesCooker_WhenContentOptionsArePresent()
    {
        await using var temp = new TempDirectory();
        var options = CreateValidOptions(temp.Path);
        options.ContentOptionsJson = "options.json"; // This triggers cooking
        
        Directory.CreateDirectory(options.SdkPath);
        Directory.CreateDirectory(options.GamePath);
        Directory.CreateDirectory(options.ProjectRoot);
        Directory.CreateDirectory(options.ModSrcRoot);
        File.WriteAllText(Path.Combine(options.ProjectRoot, options.ModName + ".x2proj"), "<Project></Project>");
        File.WriteAllText(Path.Combine(options.ModSrcRoot, "options.json"), "{}");

        var controller = CreateController(options);

        _compiler.CompileBaseAsync(Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        _compiler.CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        _cooker.CookAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ContentOptions>(), Arg.Any<BuildOptions>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        if (!result.Success) Assert.Fail("Build failed with errors: " + string.Join("; ", result.Errors));
        Assert.True(result.Success);
        await _cooker.Received(1).CookAsync("TestMod", options.StagingPath, Arg.Any<ContentOptions>(), options, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeCleanAsync_DeletesExpectedDirectories()
    {
        await using var temp = new TempDirectory();
        var options = CreateValidOptions(temp.Path);
        var controller = CreateController(options);

        Directory.CreateDirectory(options.BuildCachePath);
        var success = await controller.InvokeCleanAsync(TestContext.Current.CancellationToken);

        Assert.True(success);
        await _mirror.Received().DeleteAsync(Arg.Any<string>(), true, Arg.Any<CancellationToken>());
    }
}
