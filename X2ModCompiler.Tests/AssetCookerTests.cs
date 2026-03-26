using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using X2ModCompiler.Compilation;
using X2ModCompiler.Configuration;
using X2ModCompiler.Cooking;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Tests;

public class AssetCookerTests : TestBase
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileMirrorParity _fileMirror;
    private readonly BuildTracker _tracker;
    private readonly ILogger<AssetCooker> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public AssetCookerTests()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _fileMirror = Substitute.For<IFileMirrorParity>();
        _tracker = Substitute.For<BuildTracker>("tempPath", Substitute.For<ILogger<BuildTracker>>());
        _logger = Substitute.For<ILogger<AssetCooker>>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger<ModAssetsCookStep>>());
    }

    private AssetCooker CreateCooker(string tempPath)
    {
        return new AssetCooker(
            Path.Combine(tempPath, "SDK"),
            Path.Combine(tempPath, "Game"),
            Path.Combine(tempPath, "Cache"),
            _processRunner,
            _fileMirror,
            _tracker,
            _loggerFactory,
            _logger);
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
        await using var temp = new TempDirectory();
        var cooker = CreateCooker(temp.Path);
        
        // Arrange: No ContentForCook directory
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(temp.Path, "Src", "TestMod")
        };
        var contentOptions = new ContentOptions();

        // Act
        var result = await cooker.CookAsync("TestMod", Path.Combine(temp.Path, "Staging"), contentOptions, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        await _processRunner.DidNotReceiveWithAnyArgs().RunProcessWithSleepAsync(default!, default!, default!, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CookAsync_Skips_WhenFilesAreUpToDate()
    {
        await using var temp = new TempDirectory();
        var cooker = CreateCooker(temp.Path);
        EnsureSdkStructure(Path.Combine(temp.Path, "SDK"));

        // Arrange: Create ContentForCook with old files
        var contentForCook = Path.Combine(temp.Path, "Src", "TestMod", "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        var oldFile = Path.Combine(contentForCook, "Asset.upk");
        File.WriteAllText(oldFile, "content");
        File.SetLastWriteTime(oldFile, DateTime.Now.AddHours(-2));

        // Create fingerprint with newer timestamp
        var fingerprints = new Dictionary<string, DateTime>
        {
            { oldFile, DateTime.Now.AddHours(-1) }
        };
        var fingerprint = new BuildFingerprint("debug", "hash", DateTime.Now, DateTime.Now, fingerprints, new Dictionary<string, DateTime>());
        _tracker.LoadFingerprintAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(fingerprint)!);

        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(temp.Path, "Src", "TestMod")
        };
        var contentOptions = new ContentOptions();

        // Act
        var result = await cooker.CookAsync("TestMod", Path.Combine(temp.Path, "Staging"), contentOptions, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        await _processRunner.DidNotReceiveWithAnyArgs().RunProcessWithSleepAsync(default!, default!, default!, default, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CookAsync_RunsCooker_WhenFilesAreNewer()
    {
        await using var temp = new TempDirectory();
        var cooker = CreateCooker(temp.Path);
        EnsureSdkStructure(Path.Combine(temp.Path, "SDK"));

        // Arrange: Create ContentForCook with files
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = temp.Path,
            SdkPath = Path.Combine(temp.Path, "SDK"),
            ModDestinationPath = Path.Combine(temp.Path, "Dest")
        };
        var contentForCook = Path.Combine(options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        var newFile = Path.Combine(contentForCook, "Asset.upk");
        File.WriteAllText(newFile, "content");

        // Return null fingerprint to force cooking
        _tracker.LoadFingerprintAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new BuildFingerprint("", "", DateTime.MinValue, DateTime.MinValue, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>())));
        _processRunner.RunProcessWithSleepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputReceiver>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));

        var contentOptions = new ContentOptions { SfMaps = new List<string> { "Asset" } };

        // Act
        var result = await cooker.CookAsync("TestMod", Path.Combine(temp.Path, "Staging"), contentOptions, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        await _processRunner.Received(1).RunProcessWithSleepAsync(
            Path.Combine(temp.Path, "SDK", "binaries", "Win64", "XComGame.com"),
            Arg.Is<string>(args => args.Contains("CookPackages") && args.Contains("Asset") && args.Contains("TestMod")),
            Arg.Any<OutputReceiver>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CookAsync_UsesFinalRelease_WhenEnabled()
    {
        await using var temp = new TempDirectory();
        var cooker = CreateCooker(temp.Path);
        EnsureSdkStructure(Path.Combine(temp.Path, "SDK"));

        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = temp.Path,
            SdkPath = Path.Combine(temp.Path, "SDK"),
            ModDestinationPath = Path.Combine(temp.Path, "Dest"),
            FinalRelease = true
        };
        var contentForCook = Path.Combine(options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        File.WriteAllText(Path.Combine(contentForCook, "Asset.upk"), "content");

        // Return null fingerprint to force cooking
        _tracker.LoadFingerprintAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new BuildFingerprint("", "", DateTime.MinValue, DateTime.MinValue, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>())));
        _processRunner.RunProcessWithSleepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputReceiver>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));

        var contentOptions = new ContentOptions { SfMaps = new List<string> { "Asset" } };

        // Act
        var result = await cooker.CookAsync("TestMod", Path.Combine(temp.Path, "Staging"), contentOptions, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        await _processRunner.Received(1).RunProcessWithSleepAsync(
            Arg.Any<string>(),
            Arg.Is<string>(args => args.Contains("-final_release")),
            Arg.Any<OutputReceiver>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CookAsync_CreatesCollectionMaps_WhenDefined()
    {
        await using var temp = new TempDirectory();
        var cooker = CreateCooker(temp.Path);
        EnsureSdkStructure(Path.Combine(temp.Path, "SDK"));

        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = temp.Path,
            SdkPath = Path.Combine(temp.Path, "SDK"),
            ModDestinationPath = Path.Combine(temp.Path, "Dest"),
            BuildCachePathOverride = Path.Combine(temp.Path, "Cache")
        };
        var contentForCook = Path.Combine(options.ModSrcRoot, "ContentForCook");
        var gpcdPath = Path.Combine(options.SdkPath, "XComGame", "CookedPCConsole", "GlobalPersistentCookerData.upk");
        Directory.CreateDirectory(Path.GetDirectoryName(gpcdPath)!);
        File.WriteAllText(gpcdPath, "");
        Directory.CreateDirectory(contentForCook);
        
        // Return null fingerprint to force cooking
        _tracker.LoadFingerprintAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new BuildFingerprint("", "", DateTime.MinValue, DateTime.MinValue, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>())));
        _processRunner.RunProcessWithSleepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputReceiver>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));

        var contentOptions = new ContentOptions
        {
            SfMaps = new List<string> { "Asset" },
            SfCollectionMaps = new List<CollectionMapDefinition>
            {
                new CollectionMapDefinition { Name = "CollectionMap1", Packages = new List<string> { "Pkg1", "Pkg2" } }
            }
        };

        // Act
        var result = await cooker.CookAsync("TestMod", Path.Combine(temp.Path, "Staging"), contentOptions, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        // Verify collection maps directory was created in cooker's build cache path (temp.Path/Cache)
        Assert.True(Directory.Exists(Path.Combine(temp.Path, "Cache", "CollectionMaps")));
    }

    [Fact]
    public async Task CookAsync_StagesArtifacts_AfterSuccessfulCook()
    {
        await using var temp = new TempDirectory();
        var cooker = CreateCooker(temp.Path);
        EnsureSdkStructure(Path.Combine(temp.Path, "SDK"));

        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = temp.Path,
            SdkPath = Path.Combine(temp.Path, "SDK"),
            ModDestinationPath = Path.Combine(temp.Path, "Dest"),
            BuildCachePathOverride = Path.Combine(temp.Path, "Cache")
        };
        var contentForCook = Path.Combine(options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        var sourcePackage = Path.Combine(contentForCook, "Asset.upk");
        File.WriteAllText(sourcePackage, "content");
        File.SetLastWriteTime(sourcePackage, DateTime.Now.AddHours(-1));

        _tracker.LoadFingerprintAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new BuildFingerprint("", "", DateTime.MinValue, DateTime.MinValue, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>())));
        _processRunner.RunProcessWithSleepAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputReceiver>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(0));

        // Pre-create the cooked output in SDK to simulate successful cook
        var sdkCookedPath = options.CookerOutputPath;
        Directory.CreateDirectory(sdkCookedPath);
        var cookedPackage = Path.Combine(sdkCookedPath, "Asset_SF.upk");
        File.WriteAllText(cookedPackage, "cooked content");
        File.SetLastWriteTime(cookedPackage, DateTime.Now);
        
        var contentOptions = new ContentOptions { SfStandalone = new List<string> { "Asset" } };
        var stagingPath = Path.Combine(temp.Path, "Staging");

        // Provide the tracker JSON so it doesn't clean up our pre-created cooked file
        var cachePath = options.BuildCachePathOverride;
        Directory.CreateDirectory(cachePath);
        var trackerPath = Path.Combine(cachePath, "AssetsCookerOutputTracker.json");
        var tracker = new CookerOutputTracker();
        tracker.SfPackages.Add(new SfPackageData { FullFileName = "Asset_SF.upk", LastUpdatedUtc = File.GetLastWriteTimeUtc(cookedPackage).Ticks });
        File.WriteAllText(trackerPath, System.Text.Json.JsonSerializer.Serialize(tracker));

        // Act
        var result = await cooker.CookAsync("TestMod", stagingPath, contentOptions, options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        var stagedFile = Path.Combine(stagingPath, "CookedPCConsole", "Asset.upk");
        Assert.True(File.Exists(stagedFile), $"Staged file not found at: {stagedFile}. SDK Cooked Path was: {cookedPackage}");
        Assert.Equal("cooked content", File.ReadAllText(stagedFile));
    }
}
