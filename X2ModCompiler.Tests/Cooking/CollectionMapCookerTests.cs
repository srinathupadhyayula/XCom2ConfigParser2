using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using X2ModCompiler.Cooking;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Tests.Cooking;

/// <summary>
/// Tests for CollectionMapCooker extracted from ModAssetsCookStep.
/// </summary>
public class CollectionMapCookerTests
{
    private readonly string _tempDir;
    private readonly BuildOptions _options;
    private readonly ContentOptions _contentOptions;
    private readonly ILogger<CollectionMapCooker> _logger;

    public CollectionMapCookerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);

        var projectRoot = Path.Combine(_tempDir, "Project");
        Directory.CreateDirectory(projectRoot);
        
        // Create the mod source directory structure
        var modSrcRoot = Path.Combine(projectRoot, "TestMod");
        Directory.CreateDirectory(modSrcRoot);

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

        _contentOptions = new ContentOptions();
        _logger = Substitute.For<ILogger<CollectionMapCooker>>();
    }

    [Fact]
    public void Constructor_CreatesInstance_WithValidParameters()
    {
        // Arrange & Act
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);

        // Assert
        Assert.NotNull(cooker);
    }

    [Fact]
    public void GetCollectionMapsPath_ReturnsCorrectPath_InBuildCache()
    {
        // Arrange
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);

        // Act
        var path = cooker.GetCollectionMapsPath();

        // Assert
        Assert.Equal(Path.Combine(_options.BuildCachePath, "CollectionMaps"), path);
    }

    [Fact]
    public void InitializeCollectionMapsPath_CreatesDirectory_IfNotExists()
    {
        // Arrange
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);
        var mapsPath = cooker.GetCollectionMapsPath();

        // Act
        cooker.InitializeCollectionMapsPath();

        // Assert
        Assert.True(Directory.Exists(mapsPath));
    }

    [Fact]
    public void InitializeCollectionMapsPath_DeletesExistingDirectory_First()
    {
        // Arrange
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);
        var mapsPath = cooker.GetCollectionMapsPath();
        Directory.CreateDirectory(mapsPath);
        File.WriteAllText(Path.Combine(mapsPath, "test.txt"), "content");

        // Act
        cooker.InitializeCollectionMapsPath();

        // Assert
        Assert.True(Directory.Exists(mapsPath));
        Assert.Empty(Directory.GetFiles(mapsPath));
    }

    [Fact]
    public void GetSfCollectionOnlyMaps_ReturnsEmptyList_WhenAllMapsExist()
    {
        // Arrange
        var contentForCook = Path.Combine(_options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        
        // Create map files that match SF collection maps
        _contentOptions.SfCollectionMaps.Add(new CollectionMapDefinition 
        { 
            Name = "TestMap", 
            Packages = new List<string>() 
        });
        File.WriteAllText(Path.Combine(contentForCook, "TestMap.umap"), "content");
        
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);

        // Act
        var collectionOnlyMaps = cooker.GetSfCollectionOnlyMaps(contentForCook);

        // Assert
        Assert.Empty(collectionOnlyMaps);
    }

    [Fact]
    public void GetSfCollectionOnlyMaps_ReturnsMaps_WhenMapsDoNotExist()
    {
        // Arrange
        var contentForCook = Path.Combine(_options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        
        // Add collection map definition without creating the file
        _contentOptions.SfCollectionMaps.Add(new CollectionMapDefinition 
        { 
            Name = "MissingMap", 
            Packages = new List<string>() 
        });
        
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);

        // Act
        var collectionOnlyMaps = cooker.GetSfCollectionOnlyMaps(contentForCook);

        // Assert
        Assert.NotEmpty(collectionOnlyMaps);
        Assert.Contains("MissingMap", collectionOnlyMaps);
    }

    [Fact]
    public async Task ExtractEmptyUMapAsync_CreatesValidUMapFile_AtDestination()
    {
        // Arrange
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);
        var destinationPath = Path.Combine(_tempDir, "TestMap.umap");

        // Act
        await cooker.ExtractEmptyUMapAsync(destinationPath);

        // Assert
        Assert.True(File.Exists(destinationPath));
        Assert.True(new FileInfo(destinationPath).Length > 0);
    }

    [Fact]
    public async Task CreateTemporaryCollectionMaps_CreatesFiles_ForAllCollectionOnlyMaps()
    {
        // Arrange
        var contentForCook = Path.Combine(_options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        
        _contentOptions.SfCollectionMaps.Add(new CollectionMapDefinition 
        { 
            Name = "CollectionMap1", 
            Packages = new List<string>() 
        });
        _contentOptions.SfCollectionMaps.Add(new CollectionMapDefinition 
        { 
            Name = "CollectionMap2", 
            Packages = new List<string>() 
        });
        
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);
        var collectionOnlyMaps = cooker.GetSfCollectionOnlyMaps(contentForCook);
        cooker.InitializeCollectionMapsPath();
        var mapsPath = cooker.GetCollectionMapsPath();

        // Act
        await cooker.CreateTemporaryCollectionMapsAsync(collectionOnlyMaps);

        // Assert
        foreach (var map in collectionOnlyMaps)
        {
            var mapPath = Path.Combine(mapsPath, $"{map}.umap");
            Assert.True(File.Exists(mapPath));
        }
    }

    [Fact]
    public void DetermineDirtyMapsForCollectionMaps_ReturnsMap_WhenCookedFileDoesNotExist()
    {
        // Arrange
        var cookerOutputPath = _options.CookerOutputPath;
        Directory.CreateDirectory(cookerOutputPath);
        
        _contentOptions.SfCollectionMaps.Add(new CollectionMapDefinition 
        { 
            Name = "UncookedMap", 
            Packages = new List<string> { "Package1" } 
        });
        
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);

        // Act
        var dirtyMaps = cooker.DetermineDirtyMapsForCollectionMaps(cookerOutputPath, new List<string>());

        // Assert
        Assert.Contains("UncookedMap", dirtyMaps);
    }

    [Fact]
    public void DetermineDirtyMapsForCollectionMaps_ReturnsMap_WhenPackageIsNewer()
    {
        // Arrange
        var cookerOutputPath = _options.CookerOutputPath;
        Directory.CreateDirectory(cookerOutputPath);
        
        var contentForCook = Path.Combine(_options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        
        // Create cooked map file (older)
        var cookedMapPath = Path.Combine(cookerOutputPath, "UpdatedMap.upk");
        File.WriteAllText(cookedMapPath, "old content");
        var oldTime = DateTime.UtcNow.AddHours(-2);
        File.SetLastWriteTimeUtc(cookedMapPath, oldTime);
        
        // Create package file (newer)
        var pkgPath = Path.Combine(contentForCook, "NewPackage.upk");
        File.WriteAllText(pkgPath, "new content");
        
        _contentOptions.SfCollectionMaps.Add(new CollectionMapDefinition 
        { 
            Name = "UpdatedMap", 
            Packages = new List<string> { "NewPackage" } 
        });
        
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);

        // Act
        var dirtyMaps = cooker.DetermineDirtyMapsForCollectionMaps(cookerOutputPath, new List<string>());

        // Assert
        Assert.Contains("UpdatedMap", dirtyMaps);
    }

    [Fact]
    public void DetermineDirtyMapsForCollectionMaps_ReturnsEmptyList_WhenMapIsUpToDate()
    {
        // Arrange
        var cookerOutputPath = _options.CookerOutputPath;
        Directory.CreateDirectory(cookerOutputPath);
        
        var contentForCook = Path.Combine(_options.ModSrcRoot, "ContentForCook");
        Directory.CreateDirectory(contentForCook);
        
        // Create cooked map file (newer)
        var cookedMapPath = Path.Combine(cookerOutputPath, "UpToDateMap.upk");
        File.WriteAllText(cookedMapPath, "new content");
        
        // Create package file (older)
        var pkgPath = Path.Combine(contentForCook, "OldPackage.upk");
        File.WriteAllText(pkgPath, "old content");
        var oldTime = DateTime.UtcNow.AddHours(-2);
        File.SetLastWriteTimeUtc(pkgPath, oldTime);
        
        _contentOptions.SfCollectionMaps.Add(new CollectionMapDefinition 
        { 
            Name = "UpToDateMap", 
            Packages = new List<string> { "OldPackage" } 
        });
        
        var cooker = new CollectionMapCooker(_options, _contentOptions, _logger);

        // Act
        var dirtyMaps = cooker.DetermineDirtyMapsForCollectionMaps(cookerOutputPath, new List<string>());

        // Assert
        Assert.Empty(dirtyMaps);
    }

    private void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
