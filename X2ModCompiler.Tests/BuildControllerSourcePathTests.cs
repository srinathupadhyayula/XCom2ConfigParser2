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

/// <summary>
/// Tests for verifying correct source path handling during build.
/// </summary>
public class BuildControllerSourcePathTests : TestBase
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

    public BuildControllerSourcePathTests()
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
    /// Verifies that mod source files are copied to the correct SDK location.
    /// PowerShell behavior: Copy-Item "$includeDir\*" "$devSrcRoot\" -Force -Recurse
    /// This copies the CONTENTS of Src folder directly to SDK\Development\Src\ (merging packages)
    /// NOT nesting as SDK\Development\Src\ModName\Src\ModName\
    /// </summary>
    [Fact]
    public async Task InvokeBuildAsync_CopiesSrcFolderContents_DirectlyToSdkSrc()
    {
        await using var temp = new TempDirectory();
        // Arrange: Create nested mod structure like AdventCoalition
        // Structure: ProjectRoot\ModName\Src\ModName\Classes\*.uc
        var options = CreateValidOptionsWithNestedSrc(temp.Path);
        var controller = CreateController(options);

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

        // Act
        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        // Assert - Build should succeed (Mirror operations happen internally)
        if (!result.Success) Assert.Fail("Build failed: " + string.Join("; ", result.Errors));
        Assert.True(result.Success);
    }

    /// <summary>
    /// Verifies that for flat mod structures (Src at modSrcRoot level), the copy works correctly.
    /// PowerShell behavior: Copy-Item "$includeDir\*" "$devSrcRoot\" - copies contents, not folder itself
    /// </summary>
    [Fact]
    public async Task InvokeBuildAsync_CopiesFlatSrcStructure_Correctly()
    {
        await using var temp = new TempDirectory();
        // Arrange: Create flat mod structure where .x2proj and Src are at same level
        // This is the typical ModBuddy structure: ProjectRoot\ModName.x2proj and ProjectRoot\Src\ModName\Classes\
        var options = new BuildOptions
        {
            ModName = "FlatMod",
            ProjectRoot = Path.Combine(temp.Path, "FlatMod"),
            SdkPath = Path.Combine(temp.Path, "SDK"),
            GamePath = Path.Combine(temp.Path, "Game"),
            ModDestinationPath = Path.Combine(temp.Path, "Dest")
        };

        // For flat structure: ModSrcRoot = ProjectRoot\ModName
        // Src folder should be at ModSrcRoot\Src\ModName\Classes\
        var modSrcRoot = options.ModSrcRoot;  // = ProjectRoot\ModName
        Directory.CreateDirectory(modSrcRoot);

        var srcFolder = Path.Combine(modSrcRoot, "Src", options.ModName, "Classes");
        Directory.CreateDirectory(srcFolder);
        File.WriteAllText(Path.Combine(srcFolder, "Test.uc"), "class Test;");

        File.WriteAllText(Path.Combine(modSrcRoot, "FlatMod.x2proj"), "<Project></Project>");

        Directory.CreateDirectory(options.SdkPath);
        Directory.CreateDirectory(options.GamePath);

        var controller = CreateController(options);

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

        // Act
        var result = await controller.InvokeBuildAsync(TestContext.Current.CancellationToken);

        // Verify build succeeds (Mirror operations happen internally)
        if (!result.Success) Assert.Fail("Build failed: " + string.Join("; ", result.Errors));
        Assert.True(result.Success);
    }

    private BuildOptions CreateValidOptionsWithNestedSrc(string tempPath)
    {
        var options = new BuildOptions
        {
            ModName = "AdventCoalition",
            ProjectRoot = Path.Combine(tempPath, "AdventCoalition"),
            SdkPath = Path.Combine(tempPath, "SDK"),
            GamePath = Path.Combine(tempPath, "Game"),
            ModDestinationPath = Path.Combine(tempPath, "Dest")
        };

        // Create nested structure: ProjectRoot\ModName\Src\ModName\Classes\*.uc
        var modSrcRoot = Path.Combine(options.ProjectRoot, options.ModName);
        Directory.CreateDirectory(modSrcRoot);
        
        var srcFolder = Path.Combine(modSrcRoot, "Src", options.ModName, "Classes");
        Directory.CreateDirectory(srcFolder);
        File.WriteAllText(Path.Combine(srcFolder, "Test.uc"), "class Test;");
        
        File.WriteAllText(Path.Combine(modSrcRoot, "ModName.x2proj"), "<Project></Project>");

        Directory.CreateDirectory(options.SdkPath);
        Directory.CreateDirectory(options.GamePath);

        return options;
    }

    private BuildOptions CreateValidOptionsWithFlatSrc(string tempPath)
    {
        var options = new BuildOptions
        {
            ModName = "FlatMod",
            ProjectRoot = Path.Combine(tempPath, "FlatMod"),  // Project root = mod root for flat structure
            SdkPath = Path.Combine(tempPath, "SDK"),
            GamePath = Path.Combine(tempPath, "Game"),
            ModDestinationPath = Path.Combine(tempPath, "Dest")
        };

        // Create flat structure: ProjectRoot\Src\ModName\Classes\*.uc
        // For flat mods, ModSrcRoot = ProjectRoot (no nested folder)
        var modSrcRoot = options.ProjectRoot;
        var srcFolder = Path.Combine(modSrcRoot, "Src", options.ModName, "Classes");
        Directory.CreateDirectory(srcFolder);
        File.WriteAllText(Path.Combine(srcFolder, "Test.uc"), "class Test;");
        
        File.WriteAllText(Path.Combine(modSrcRoot, "FlatMod.x2proj"), "<Project></Project>");

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
