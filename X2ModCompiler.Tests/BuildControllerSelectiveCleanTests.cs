using System;
using System.Collections.Generic;
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

/// <summary>
/// Tests for selective clean integration (CheckCleanCompiled functionality).
/// </summary>
public class BuildControllerSelectiveCleanTests : TestBase
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

    public BuildControllerSelectiveCleanTests()
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

    /// <summary>
    /// Verifies that selective clean is triggered when build mode changes (debug <-> release).
    /// </summary>
    [Fact]
    public async Task InvokeBuildAsync_PerformsSelectiveClean_WhenBuildModeChanges()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var options = CreateValidOptions(temp.Path);
        options.Debug = false; // release mode
        
        // Setup tracker to return debug fingerprint (mode switch)
        var oldFingerprint = new BuildFingerprint("debug", "hash123", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());
        _tracker.LoadFingerprintAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(oldFingerprint));
        _tracker.ShouldRebuildAsync(Arg.Any<BuildOptions>(), Arg.Any<string>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _tracker.GetSelectiveCleanPathsAsync(Arg.Any<string>(), Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<string> { "test.u" }));

        var controller = CreateController(options);

        _compiler.CompileBaseAsync(Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _compiler.CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        // Assert - Verify selective clean was triggered (ShouldRebuildAsync was called)
        await _tracker.Received(1).ShouldRebuildAsync(
            Arg.Any<BuildOptions>(),
            Arg.Any<string>(),
            Arg.Any<DateTime?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that selective clean is NOT triggered when nothing changed.
    /// </summary>
    [Fact]
    public async Task InvokeBuildAsync_DoesNotPerformSelectiveClean_WhenNothingChanged()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var options = CreateValidOptions(temp.Path);
        options.Debug = true; // same mode as fingerprint
        
        // Setup tracker to return same fingerprint (no changes)
        var currentFingerprint = new BuildFingerprint("debug", "hash123", DateTime.UtcNow, DateTime.UtcNow, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());
        _tracker.LoadFingerprintAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(currentFingerprint));
        _tracker.ShouldRebuildAsync(Arg.Any<BuildOptions>(), Arg.Any<string>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var controller = CreateController(options);

        _compiler.CompileBaseAsync(Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        _compiler.CompileModAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<BuildOptions>(), Arg.Any<OutputReceiver>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Act
        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        // Assert - Verify selective clean was NOT triggered
        await _mirror.DidNotReceive().DeleteAsync(
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    private BuildOptions CreateValidOptions(string tempPath)
    {
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(tempPath, "TestMod"),
            SdkPath = Path.Combine(tempPath, "SDK"),
            GamePath = Path.Combine(tempPath, "Game"),
            ModDestinationPath = Path.Combine(tempPath, "Dest")
        };

        var modSrcRoot = Path.Combine(options.ProjectRoot, options.ModName);
        Directory.CreateDirectory(modSrcRoot);
        
        var srcFolder = Path.Combine(modSrcRoot, "Src", options.ModName, "Classes");
        Directory.CreateDirectory(srcFolder);
        File.WriteAllText(Path.Combine(srcFolder, "Test.uc"), "class Test;");
        
        File.WriteAllText(Path.Combine(modSrcRoot, "TestMod.x2proj"), "<Project></Project>");

        Directory.CreateDirectory(options.SdkPath);
        Directory.CreateDirectory(options.GamePath);

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
