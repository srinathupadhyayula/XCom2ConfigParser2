using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using XCom2ModCompiler.Compilation;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Cooking;
using XCom2ModCompiler.Tracking;
using XCom2ModCompiler.Utilities;

namespace XCom2ModCompiler.Tests;

public class AssetCookerTests : IDisposable
{
    private readonly Mock<IProcessRunner> _processRunnerMock;
    private readonly Mock<IFileMirror> _fileMirrorMock;
    private readonly Mock<BuildTracker> _trackerMock;
    private readonly Mock<ILogger<AssetCooker>> _loggerMock;
    private readonly AssetCooker _cooker;
    private readonly string _tempPath;

    public AssetCookerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);

        _processRunnerMock = new Mock<IProcessRunner>();
        _fileMirrorMock = new Mock<IFileMirror>();
        _trackerMock = new Mock<BuildTracker>(_tempPath, new Mock<ILogger<BuildTracker>>().Object);
        _loggerMock = new Mock<ILogger<AssetCooker>>();
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger<ModAssetsCookStep>>().Object);

        _cooker = new AssetCooker(
            Path.Combine(_tempPath, "SDK"),
            Path.Combine(_tempPath, "Game"),
            Path.Combine(_tempPath, "Cache"),
            _processRunnerMock.Object,
            _fileMirrorMock.Object,
            _trackerMock.Object,
            loggerFactoryMock.Object,
            _loggerMock.Object);
        
        EnsureSdkStructure(Path.Combine(_tempPath, "SDK"));
    }

    private void EnsureSdkStructure(string sdkPath)
    {
        var configPath = Path.Combine(sdkPath, "XComGame", "Config");
        Directory.CreateDirectory(configPath);
        File.WriteAllText(Path.Combine(configPath, "DefaultEngine.ini"), "[Engine.Engine]\n+EditPackages=Core");

        var cookedPCPath = Path.Combine(sdkPath, "XComGame", "CookedPCConsole");
        Directory.CreateDirectory(cookedPCPath);
        File.WriteAllText(Path.Combine(cookedPCPath, "GlobalPersistentCookerData.upk"), "");

        var binariesPath = Path.Combine(sdkPath, "binaries", "Win64");
        Directory.CreateDirectory(binariesPath);
        File.WriteAllText(Path.Combine(binariesPath, "XComGame.com"), "");
    }

    [Fact]
    public async Task CookAsync_ReturnsTrue_WhenContentForCookNotFound()
    {
        // Arrange: No ContentForCook directory
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(_tempPath, "Src", "TestMod")
        };
        var contentOptions = new ContentOptions();

        // Act
        var result = await _cooker.CookAsync("TestMod", Path.Combine(_tempPath, "Staging"), contentOptions, options, CancellationToken.None);

        // Assert
        Assert.True(result);
        _processRunnerMock.Verify(r => r.RunProcessWithSleepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OutputReceiver>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CookAsync_Skips_WhenFilesAreUpToDate()
    {
        // Arrange: Create ContentForCook with old files
        var contentForCook = Path.Combine(_tempPath, "Src", "TestMod", "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        var oldFile = Path.Combine(contentForCook, "Asset.upk");
        File.WriteAllText(oldFile, "content");
        File.SetLastWriteTime(oldFile, DateTime.Now.AddHours(-2));

        // Create fingerprint with newer timestamp
        var fingerprints = new Dictionary<string, DateTime>
        {
            { oldFile, DateTime.Now.AddHours(-1) }
        };
        var fingerprint = new BuildFingerprint("debug", "hash", DateTime.Now, DateTime.Now, fingerprints);
        _trackerMock.Setup(t => t.LoadFingerprintAsync(It.IsAny<CancellationToken>())).ReturnsAsync(fingerprint);

        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(_tempPath, "Src", "TestMod")
        };
        var contentOptions = new ContentOptions();

        // Act
        var result = await _cooker.CookAsync("TestMod", Path.Combine(_tempPath, "Staging"), contentOptions, options, CancellationToken.None);

        // Assert
        Assert.True(result);
        _processRunnerMock.Verify(r => r.RunProcessWithSleepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OutputReceiver>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CookAsync_RunsCooker_WhenFilesAreNewer()
    {
        // Arrange: Create ContentForCook with files
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = _tempPath,
            SdkPath = Path.Combine(_tempPath, "SDK"),
            ModDestinationPath = Path.Combine(_tempPath, "Dest")
        };
        var contentForCook = Path.Combine(options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        var newFile = Path.Combine(contentForCook, "Asset.upk");
        File.WriteAllText(newFile, "content");

        // Return null fingerprint to force cooking
        _trackerMock.Setup(t => t.LoadFingerprintAsync(It.IsAny<CancellationToken>())).ReturnsAsync((BuildFingerprint)null!);
        _processRunnerMock.Setup(r => r.RunProcessWithSleepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OutputReceiver>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var contentOptions = new ContentOptions { SfMaps = new List<string> { "Asset" } };

        // Act
        var result = await _cooker.CookAsync("TestMod", Path.Combine(_tempPath, "Staging"), contentOptions, options, CancellationToken.None);

        // Assert
        Assert.True(result);
        _processRunnerMock.Verify(r => r.RunProcessWithSleepAsync(
            Path.Combine(_tempPath, "SDK", "binaries", "Win64", "XComGame.com"),
            It.Is<string>(args => args.Contains("CookPackages") && args.Contains("Asset") && args.Contains("TestMod")),
            It.IsAny<OutputReceiver>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CookAsync_UsesFinalRelease_WhenEnabled()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = _tempPath,
            SdkPath = Path.Combine(_tempPath, "SDK"),
            ModDestinationPath = Path.Combine(_tempPath, "Dest"),
            FinalRelease = true
        };
        var contentForCook = Path.Combine(options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        File.WriteAllText(Path.Combine(contentForCook, "Asset.upk"), "content");

        // Return null fingerprint to force cooking
        _trackerMock.Setup(t => t.LoadFingerprintAsync(It.IsAny<CancellationToken>())).ReturnsAsync((BuildFingerprint)null!);
        _processRunnerMock.Setup(r => r.RunProcessWithSleepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OutputReceiver>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var contentOptions = new ContentOptions { SfMaps = new List<string> { "Asset" } };

        // Act
        var result = await _cooker.CookAsync("TestMod", Path.Combine(_tempPath, "Staging"), contentOptions, options, CancellationToken.None);

        // Assert
        Assert.True(result);
        _processRunnerMock.Verify(r => r.RunProcessWithSleepAsync(
            It.IsAny<string>(),
            It.Is<string>(args => args.Contains("-final_release")),
            It.IsAny<OutputReceiver>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CookAsync_CreatesCollectionMaps_WhenDefined()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = _tempPath,
            SdkPath = Path.Combine(_tempPath, "SDK"),
            ModDestinationPath = Path.Combine(_tempPath, "Dest"),
            BuildCachePathOverride = Path.Combine(_tempPath, "Cache")
        };
        var contentForCook = Path.Combine(options.ModSrcRoot, "ContentForCook");
        var gpcdPath = Path.Combine(options.SdkPath, "XComGame", "CookedPCConsole", "GlobalPersistentCookerData.upk");
        Directory.CreateDirectory(Path.GetDirectoryName(gpcdPath)!);
        File.WriteAllText(gpcdPath, "");
        Directory.CreateDirectory(contentForCook);
        
        // Return null fingerprint to force cooking
        _trackerMock.Setup(t => t.LoadFingerprintAsync(It.IsAny<CancellationToken>())).ReturnsAsync((BuildFingerprint)null!);
        _processRunnerMock.Setup(r => r.RunProcessWithSleepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OutputReceiver>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var contentOptions = new ContentOptions
        {
            SfMaps = new List<string> { "Asset" },
            SfCollectionMaps = new List<CollectionMapDefinition>
            {
                new CollectionMapDefinition { Name = "CollectionMap1", Packages = new List<string> { "Pkg1", "Pkg2" } }
            }
        };

        // Act
        var result = await _cooker.CookAsync("TestMod", Path.Combine(_tempPath, "Staging"), contentOptions, options, CancellationToken.None);

        // Assert
        Assert.True(result);
        // Verify collection maps directory was created in cooker's build cache path (_tempPath/Cache)
        Assert.True(Directory.Exists(Path.Combine(_tempPath, "Cache", "CollectionMaps")));
    }

    [Fact]
    public async Task CookAsync_StagesArtifacts_AfterSuccessfulCook()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = _tempPath,
            SdkPath = Path.Combine(_tempPath, "SDK"),
            ModDestinationPath = Path.Combine(_tempPath, "Dest"),
            BuildCachePathOverride = Path.Combine(_tempPath, "Cache")
        };
        var contentForCook = Path.Combine(options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        var sourcePackage = Path.Combine(contentForCook, "Asset.upk");
        File.WriteAllText(sourcePackage, "content");
        File.SetLastWriteTime(sourcePackage, DateTime.Now.AddHours(-1));

        _trackerMock.Setup(t => t.LoadFingerprintAsync(It.IsAny<CancellationToken>())).ReturnsAsync((BuildFingerprint)null!);
        _processRunnerMock.Setup(r => r.RunProcessWithSleepAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<OutputReceiver>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        // Pre-create the cooked output in SDK to simulate successful cook
        var sdkCookedPath = options.CookerOutputPath;
        Directory.CreateDirectory(sdkCookedPath);
        var cookedPackage = Path.Combine(sdkCookedPath, "Asset_SF.upk");
        File.WriteAllText(cookedPackage, "cooked content");
        File.SetLastWriteTime(cookedPackage, DateTime.Now);
        
        var contentOptions = new ContentOptions { SfStandalone = new List<string> { "Asset" } };
        var stagingPath = Path.Combine(_tempPath, "Staging");

        // Provide the tracker JSON so it doesn't clean up our pre-created cooked file
        var cachePath = options.BuildCachePathOverride;
        Directory.CreateDirectory(cachePath);
        var trackerPath = Path.Combine(cachePath, "AssetsCookerOutputTracker.json");
        var tracker = new CookerOutputTracker();
        tracker.SfPackages.Add(new SfPackageData { FullFileName = "Asset_SF.upk", LastUpdatedUtc = File.GetLastWriteTimeUtc(cookedPackage).Ticks });
        File.WriteAllText(trackerPath, System.Text.Json.JsonSerializer.Serialize(tracker));

        // Act
        var result = await _cooker.CookAsync("TestMod", stagingPath, contentOptions, options, CancellationToken.None);

        // Assert
        Assert.True(result);
        var stagedFile = Path.Combine(stagingPath, "CookedPCConsole", "Asset.upk");
        Assert.True(File.Exists(stagedFile), $"Staged file not found at: {stagedFile}. SDK Cooked Path was: {cookedPackage}");
        Assert.Equal("cooked content", File.ReadAllText(stagedFile));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }
}
