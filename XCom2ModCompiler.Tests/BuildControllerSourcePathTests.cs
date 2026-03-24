using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

/// <summary>
/// Tests for verifying correct source path handling during build.
/// </summary>
public class BuildControllerSourcePathTests : IDisposable
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

    public BuildControllerSourcePathTests()
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

    /// <summary>
    /// Verifies that mod source files are copied to the correct SDK location.
    /// PowerShell behavior: Copy-Item "$includeDir\*" "$devSrcRoot\" -Force -Recurse
    /// This copies the CONTENTS of Src folder directly to SDK\Development\Src\ (merging packages)
    /// NOT nesting as SDK\Development\Src\ModName\Src\ModName\
    /// </summary>
    [Fact]
    public async Task InvokeBuildAsync_CopiesSrcFolderContents_DirectlyToSdkSrc()
    {
        // Arrange: Create nested mod structure like AdventCoalition
        // Structure: ProjectRoot\ModName\Src\ModName\Classes\*.uc
        var options = CreateValidOptionsWithNestedSrc();
        var controller = CreateController(options);

        _compilerMock.Setup(x => x.CompileBaseAsync(It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _compilerMock.Setup(x => x.CompileModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await controller.InvokeBuildAsync();

        // Assert - Verify Src folder CONTENTS are copied directly to SDK\Development\Src\
        // PowerShell: Copy-Item "$($this.modSrcRoot)\Src\*" "$($this.devSrcRoot)\"
        // This means: ModDir\Src\ModName\Classes\*.uc → SDK\Src\ModName\Classes\*.uc
        _mirrorMock.Verify(m => m.MirrorAsync(
            Path.Combine(options.ModSrcRoot, "Src", options.ModName),  // Source: ModSrcRoot\Src\ModName\
            Path.Combine(options.SdkPath, "Development", "Src", options.ModNameCanonical),  // Dest: SDK\Development\Src\ModName
            It.IsAny<string>(),
            It.IsAny<string[]?>(),
            It.IsAny<string[]?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that for flat mod structures (Src at modSrcRoot level), the copy works correctly.
    /// PowerShell behavior: Copy-Item "$includeDir\*" "$devSrcRoot\" - copies contents, not folder itself
    /// </summary>
    [Fact]
    public async Task InvokeBuildAsync_CopiesFlatSrcStructure_Correctly()
    {
        // Arrange: Create flat mod structure where .x2proj and Src are at same level
        // This is the typical ModBuddy structure: ProjectRoot\ModName.x2proj and ProjectRoot\Src\ModName\Classes\
        var options = new BuildOptions
        {
            ModName = "FlatMod",
            ProjectRoot = Path.Combine(_tempPath, "FlatMod"),
            SdkPath = Path.Combine(_tempPath, "SDK"),
            GamePath = Path.Combine(_tempPath, "Game"),
            ModDestinationPath = Path.Combine(_tempPath, "Dest")
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

        _compilerMock.Setup(x => x.CompileBaseAsync(It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _compilerMock.Setup(x => x.CompileModAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BuildOptions>(), It.IsAny<OutputReceiver>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await controller.InvokeBuildAsync();

        // Assert - Verify Src folder CONTENTS are copied correctly
        // PowerShell: Copy-Item "$($this.modSrcRoot)\Src\*" "$($this.devSrcRoot)\"
        _mirrorMock.Verify(m => m.MirrorAsync(
            Path.Combine(options.ModSrcRoot, "Src", options.ModName),  // Source: ModSrcRoot\Src\ModName\
            Path.Combine(options.SdkPath, "Development", "Src", options.ModNameCanonical),  // Dest: SDK\Development\Src\ModName
            It.IsAny<string>(),
            It.IsAny<string[]?>(),
            It.IsAny<string[]?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private BuildOptions CreateValidOptionsWithNestedSrc()
    {
        var options = new BuildOptions
        {
            ModName = "AdventCoalition",
            ProjectRoot = Path.Combine(_tempPath, "AdventCoalition"),
            SdkPath = Path.Combine(_tempPath, "SDK"),
            GamePath = Path.Combine(_tempPath, "Game"),
            ModDestinationPath = Path.Combine(_tempPath, "Dest")
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

    private BuildOptions CreateValidOptionsWithFlatSrc()
    {
        var options = new BuildOptions
        {
            ModName = "FlatMod",
            ProjectRoot = Path.Combine(_tempPath, "FlatMod"),  // Project root = mod root for flat structure
            SdkPath = Path.Combine(_tempPath, "SDK"),
            GamePath = Path.Combine(_tempPath, "Game"),
            ModDestinationPath = Path.Combine(_tempPath, "Dest")
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
