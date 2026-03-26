using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Cooking;

/// <summary>
/// Handles collection map cooking operations including temporary map creation,
/// collection-only map tracking, and dirty map determination for the asset cooking pipeline.
/// </summary>
public class CollectionMapCooker
{
    private readonly BuildOptions _options;
    private readonly ContentOptions _contentOptions;
    private readonly ILogger<CollectionMapCooker> _logger;
    private readonly string _collectionMapsPath;
    private readonly string _contentForCookPath;

    /// <summary>
    /// Initializes a new instance of the CollectionMapCooker class.
    /// </summary>
    /// <param name="options">The build options containing mod configuration.</param>
    /// <param name="contentOptions">The content-specific configuration for collection maps.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public CollectionMapCooker(BuildOptions options, ContentOptions contentOptions, ILogger<CollectionMapCooker> logger)
    {
        _options = options;
        _contentOptions = contentOptions;
        _logger = logger;
        _collectionMapsPath = Path.Combine(_options.BuildCachePath, "CollectionMaps");
        _contentForCookPath = Path.Combine(_options.ModSrcRoot, "ContentForCook");
    }

    /// <summary>
    /// Gets the path to the collection maps directory in the build cache.
    /// </summary>
    /// <returns>The full path to the CollectionMaps directory.</returns>
    public string GetCollectionMapsPath() => _collectionMapsPath;

    /// <summary>
    /// Initializes the collection maps directory by creating it or deleting and recreating if it exists.
    /// </summary>
    public void InitializeCollectionMapsPath()
    {
        if (Directory.Exists(_collectionMapsPath))
        {
            Directory.Delete(_collectionMapsPath, true);
        }
        Directory.CreateDirectory(_collectionMapsPath);
    }

    /// <summary>
    /// Identifies which SF collection maps exist only as definitions without source .umap files.
    /// </summary>
    /// <param name="contentForCookPath">The path to the ContentForCook directory.</param>
    /// <returns>A list of collection-only map names that need temporary map files.</returns>
    public List<string> GetSfCollectionOnlyMaps(string contentForCookPath)
    {
        var sfCollectionOnlyMaps = new List<string>();
        
        foreach (var mapDef in _contentOptions.SfCollectionMaps)
        {
            var map = mapDef.Name;
            var found = Directory.GetFiles(contentForCookPath, $"{map}.umap", SearchOption.AllDirectories)
                .Any();
            if (!found)
            {
                sfCollectionOnlyMaps.Add(map);
            }
        }

        return sfCollectionOnlyMaps;
    }

    /// <summary>
    /// Extracts an embedded empty map resource to the specified destination path.
    /// </summary>
    /// <param name="destinationPath">The full path where the map file should be created.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExtractEmptyUMapAsync(string destinationPath)
    {
        var assembly = typeof(CollectionMapCooker).Assembly;
        var resourceName = "X2ModCompiler.Resources.EmptyUMap";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new Exception($"Embedded resource {resourceName} not found.");
        }

        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);
    }

    /// <summary>
    /// Creates temporary empty map files for all collection-only maps.
    /// </summary>
    /// <param name="collectionOnlyMaps">The list of collection-only map names.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CreateTemporaryCollectionMapsAsync(List<string> collectionOnlyMaps)
    {
        foreach (var map in collectionOnlyMaps)
        {
            var destPath = Path.Combine(_collectionMapsPath, $"{map}.umap");
            await ExtractEmptyUMapAsync(destPath);
        }
    }

    /// <summary>
    /// Determines which collection maps need to be recooked based on file timestamps.
    /// </summary>
    /// <param name="cookerOutputPath">The path to the cooker output directory.</param>
    /// <param name="alreadyDirtyMaps">Maps already identified as dirty from other checks.</param>
    /// <returns>A list of collection map names that require recooking.</returns>
    public List<string> DetermineDirtyMapsForCollectionMaps(string cookerOutputPath, List<string> alreadyDirtyMaps)
    {
        var dirtyMaps = new List<string>();

        foreach (var mapDef in _contentOptions.SfCollectionMaps)
        {
            var map = mapDef.Name;
            var cookedPath = Path.Combine(cookerOutputPath, $"{map}.upk");

            if (alreadyDirtyMaps.Contains(map)) continue;

            if (!File.Exists(cookedPath))
            {
                _logger.LogInformation("{Map} has no cooked version", map);
                dirtyMaps.Add(map);
            }
            else
            {
                var existingCooked = new FileInfo(cookedPath);

                foreach (var package in mapDef.Packages)
                {
                    var pkgFile = Directory.GetFiles(_contentForCookPath, $"{package}.upk", SearchOption.AllDirectories).FirstOrDefault();
                    if (pkgFile != null)
                    {
                        var pkgFileInfo = new FileInfo(pkgFile);
                        if (pkgFileInfo.LastWriteTime > existingCooked.LastWriteTime)
                        {
                            _logger.LogInformation("{Map} dependency was updated ({Package})", map, package);
                            dirtyMaps.Add(map);
                            break;
                        }
                    }
                }
            }
        }

        return dirtyMaps;
    }
}
