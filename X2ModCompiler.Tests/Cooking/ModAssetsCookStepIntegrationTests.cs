using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using X2ModCompiler.Cooking;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;
using X2ModCompiler.Tracking;

namespace X2ModCompiler.Tests.Cooking;

/// <summary>
/// Integration tests verifying ModAssetsCookStep uses extracted classes correctly.
/// </summary>
public class ModAssetsCookStepIntegrationTests
{
    private readonly string _tempDir;
    private readonly BuildOptions _options;
    private readonly ContentOptions _contentOptions;
    private readonly IProcessRunner _processRunner;
    private readonly IFileMirrorParity _mirror;
    private readonly BuildTracker _tracker;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ModAssetsCookStep> _logger;

    public ModAssetsCookStepIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        var projectRoot = Path.Combine(_tempDir, "Project");
        Directory.CreateDirectory(projectRoot);
        
        // Create mod source directory
        var modSrcRoot = Path.Combine(projectRoot, "TestMod");
        Directory.CreateDirectory(modSrcRoot);
        var contentForCook = Path.Combine(modSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);

        _options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = projectRoot,
            SdkPath = Path.Combine(_tempDir, "Sdk"),
            GamePath = Path.Combine(_tempDir, "Game"),
            ModDestinationPath = Path.Combine(_tempDir, "Dest"),
            BuildCachePathOverride = Path.Combine(_tempDir, "Cache")
        };

        Directory.CreateDirectory(_options.SdkPath);
        Directory.CreateDirectory(_options.GamePath);
        Directory.CreateDirectory(_options.CookerOutputPath);

        _contentOptions = new ContentOptions();
        _processRunner = Substitute.For<IProcessRunner>();
        _mirror = Substitute.For<IFileMirrorParity>();
        _tracker = Substitute.For<BuildTracker>("cachePath", Substitute.For<ILogger<BuildTracker>>());
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _logger = Substitute.For<ILogger<ModAssetsCookStep>>();
    }

    [Fact]
    public void SdkEnvironmentVerifier_Integration_VerifiesSdkEnvironment_WhenProperlyConfigured()
    {
        // Arrange - Create SDK structure
        var gpcdPath = Path.Combine(_options.SdkPath, "XComGame", "CookedPCConsole", "GlobalPersistentCookerData.upk");
        Directory.CreateDirectory(Path.GetDirectoryName(gpcdPath)!);
        File.WriteAllText(gpcdPath, "dummy");

        // Create empty SDK content mods directory
        var sdkContentMods = Path.Combine(_options.SdkPath, "XComGame", "Content", "Mods", _options.ModNameCanonical);
        Directory.CreateDirectory(sdkContentMods);

        var verifier = new SdkEnvironmentVerifier(_options);

        // Act & Assert - All verifications should pass
        var exception = Record.Exception(() =>
        {
            verifier.VerifyContentForCookExists();
            verifier.VerifySdkContentModsDirectoryEmpty();
            verifier.VerifyShippedGpcdExists();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ModAssetsCookStep_UsesTfcManager_WhenGettingTfcFiles()
    {
        // Arrange
        var step = new ModAssetsCookStep(
            _options,
            _contentOptions,
            Path.Combine(_tempDir, "Staging"),
            _processRunner,
            _mirror,
            _tracker,
            _loggerFactory,
            _logger);

        // Create TFC files with correct suffix
        var tfcSuffix = $"_{_options.ModNameCanonical}_DLCTFC_XPACK_";
        var tfc1 = Path.Combine(_options.CookerOutputPath, $"Texture1{tfcSuffix}.tfc");
        var tfc2 = Path.Combine(_options.CookerOutputPath, $"Texture2{tfcSuffix}.tfc");
        File.WriteAllText(tfc1, "content 1");
        File.WriteAllText(tfc2, "content 2");

        // Act - Get TFC files using reflection to access private method
        var method = typeof(ModAssetsCookStep).GetMethod("GetOurTfcFiles",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method?.Invoke(step, null) as FileInfo[];

        // Assert - Should find TFC files with correct suffix
        Assert.NotNull(result);
        Assert.Equal(2, result.Length);
        Assert.All(result, f => Assert.Contains(tfcSuffix, f.Name));
    }

    [Fact]
    public void CollectionMapCooker_Integration_CreatesTemporaryMaps_ForCollectionOnlyMaps()
    {
        // Arrange - Add collection map definitions without source files
        _contentOptions.SfCollectionMaps.Add(new CollectionMapDefinition
        {
            Name = "CollectionMap1",
            Packages = new List<string> { "Package1" }
        });
        _contentOptions.SfCollectionMaps.Add(new CollectionMapDefinition
        {
            Name = "CollectionMap2",
            Packages = new List<string> { "Package2" }
        });

        var cooker = new CollectionMapCooker(_options, _contentOptions, 
            _loggerFactory.CreateLogger<CollectionMapCooker>());
        
        var contentForCook = Path.Combine(_options.ModSrcRoot, "ContentForCook");

        // Act - Get collection-only maps
        var collectionOnlyMaps = cooker.GetSfCollectionOnlyMaps(contentForCook);

        // Assert - Should identify maps that don't have source files
        Assert.NotNull(collectionOnlyMaps);
        Assert.Equal(2, collectionOnlyMaps.Count);
        Assert.Contains("CollectionMap1", collectionOnlyMaps);
        Assert.Contains("CollectionMap2", collectionOnlyMaps);
    }

    [Fact]
    public async Task ModAssetsCookStep_ExecuteAsync_ReturnsTrue_WhenNoAssetsToCook()
    {
        // Arrange - Empty content options
        var step = new ModAssetsCookStep(
            _options,
            new ContentOptions(),
            Path.Combine(_tempDir, "Staging"),
            _processRunner,
            _mirror,
            _tracker,
            _loggerFactory,
            _logger);

        // Act
        var result = await step.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ModAssetsCookStep_ExecuteAsync_SkipsCooking_WhenNoDirtyMaps()
    {
        // Arrange - Create ContentForCook but no maps to cook
        var contentForCook = Path.Combine(_options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        
        // Create GPCD for SDK verification
        var gpcdPath = Path.Combine(_options.SdkPath, "XComGame", "CookedPCConsole", "GlobalPersistentCookerData.upk");
        Directory.CreateDirectory(Path.GetDirectoryName(gpcdPath)!);
        File.WriteAllText(gpcdPath, "dummy");

        // Create empty SDK content mods directory
        var sdkContentMods = Path.Combine(_options.SdkPath, "XComGame", "Content", "Mods", _options.ModNameCanonical);
        Directory.CreateDirectory(sdkContentMods);

        var step = new ModAssetsCookStep(
            _options,
            _contentOptions,
            Path.Combine(_tempDir, "Staging"),
            _processRunner,
            _mirror,
            _tracker,
            _loggerFactory,
            _logger);

        // Act
        var result = await step.ExecuteAsync(CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    private void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
