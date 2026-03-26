using System.Text;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Compilation;
using X2ModCompiler.Utilities;
using X2ModCompiler.Tracking;
using X2ModCompiler.Exceptions;

namespace X2ModCompiler.Cooking;


/// <summary>
/// Asset cooking step - mirrors PowerShell ModAssetsCookStep class.
/// Handles the complete asset cooking pipeline with incremental support.
/// </summary>
/// <summary>
/// Implements the core asset cooking pipeline for XCOM 2 mods, providing parity with the original PowerShell ModAssetsCookStep.
/// This class handles incremental cooking, SDK environment preparation, and performance tracking for TFC and SF packages.
/// </summary>
public class ModAssetsCookStep
{
    private readonly BuildOptions _project;
    private readonly ContentOptions _contentOptions;
    private readonly IProcessRunner _runner;
    private readonly IFileMirrorParity _mirror;
    private readonly BuildTracker _tracker;
    private readonly string _stagingPath;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ModAssetsCookStep> _logger;

    private string _actualTfcSuffix = "";
    private string _contentForCookPath = "";
    private string _collectionMapsPath = "";
    private string _sdkContentModsDir = "";
    private string _sdkContentModsOurDir = "";
    private List<string> _dirtyMaps = new();
    private List<string> _cookedMaps = new();
    private List<string> _sfCollectionOnlyMaps = new();
    private string _engineIniDefaultPath = "";
    private string _engineIniXComPath = "";
    private string _editorArgs = "";
    private string _cookerOutputTrackerPath = "";
    private CookerOutputTracker _cookerOutputTracker = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ModAssetsCookStep"/> class.
    /// </summary>
    /// <param name="project">The build options for the project.</param>
    /// <param name="contentOptions">The content-specific configuration for asset processing.</param>
    /// <param name="stagingPath">The path where finalized assets should be placed.</param>
    /// <param name="runner">The process runner for commandlet invocation.</param>
    /// <param name="mirror">The file mirroring service for resource syncing.</param>
    /// <param name="tracker">The build tracker for change detection.</param>
    /// <param name="logger">The logger for internal diagnostics.</param>
    public ModAssetsCookStep(
        BuildOptions project,
        ContentOptions contentOptions,
        string stagingPath,
        IProcessRunner runner,
        IFileMirrorParity mirror,
        BuildTracker tracker,
        ILoggerFactory loggerFactory,
        ILogger<ModAssetsCookStep> logger)
    {
        _project = project;
        _contentOptions = contentOptions;
        _stagingPath = stagingPath;
        _runner = runner;
        _mirror = mirror;
        _tracker = tracker;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the complete asset cooking sequence, including environment setup, change detection, and commandlet execution.
    /// </summary>
    /// <param name="ct">A cancellation token to abort the process.</param>
    /// <returns>True if the cooking pipeline completed successfully; otherwise false.</returns>
    public async Task<bool> ExecuteAsync(CancellationToken ct)
    {
        if (!AnyAssetsToCook())
        {
            _logger.LogInformation("No asset cooking is requested, skipping");
            return true;
        }

        _logger.LogInformation("Initializing assets cooking");
        Init();
        VerifyProjectAndSdk();

        _logger.LogInformation("Preparing assets cooking");
        await PrepareSdkFoldersAsync(ct);
        await PrepareProjectCacheAsync();

        VerifyCachedTfcsNotAltered();
        VerifyCachedSfPackagesNotAltered();

        DetermineDirtyMaps();
        StageArtifacts();

        if (!_dirtyMaps.Any())
        {
            _logger.LogInformation("No maps are dirty, skipping cooking");
            return true;
        }

        PrepareEngineIni();
        PrepareEditorArgs();

        _logger.LogInformation("Starting assets cooking");
        await ExecuteCoreAsync(ct);
        WarnTfcGrowth();
        RecordCookerOutputTracker();

        _logger.LogInformation("Assets cook completed");
        return true;
    }

    /// <summary>
    /// Initializes internal paths, suffixes, and map lists based on project configuration.
    /// </summary>
    private void Init()
    {
        _actualTfcSuffix = $"_{_project.ModNameCanonical}_DLCTFC_XPACK_";
        _contentForCookPath = Path.Combine(_project.ModSrcRoot, "ContentForCook");
        _collectionMapsPath = Path.Combine(_project.BuildCachePath, "CollectionMaps");
        _sdkContentModsDir = Path.Combine(_project.SdkPath, "XComGame", "Content", "Mods");
        _sdkContentModsOurDir = Path.Combine(_sdkContentModsDir, _project.ModNameCanonical);

        _cookedMaps = new List<string>(_contentOptions.SfMaps);
        foreach (var mapDef in _contentOptions.SfCollectionMaps)
        {
            _cookedMaps.Add(mapDef.Name);
        }

        _sfCollectionOnlyMaps = new List<string>();
        foreach (var mapDef in _contentOptions.SfCollectionMaps)
        {
            var map = mapDef.Name;
            var found = Directory.GetFiles(_contentForCookPath, $"{map}.umap", SearchOption.AllDirectories)
                .Any();
            if (!found)
            {
                _sfCollectionOnlyMaps.Add(map);
            }
        }

        _cookerOutputTrackerPath = Path.Combine(_project.BuildCachePath, "AssetsCookerOutputTracker.json");
        if (File.Exists(_cookerOutputTrackerPath))
        {
            var json = File.ReadAllText(_cookerOutputTrackerPath);
            _cookerOutputTracker = System.Text.Json.JsonSerializer.Deserialize<CookerOutputTracker>(json) ?? new CookerOutputTracker();
        }
        else
        {
            _cookerOutputTracker = new CookerOutputTracker();
        }
    }

    /// <summary>
    /// Validates that the project source and SDK environment are in a consistent state for cooking.
    /// </summary>
    /// <exception cref="BuildFailureException">Thrown if core directories or artifacts are missing.</exception>
    private void VerifyProjectAndSdk()
    {
        if (!Directory.Exists(_contentForCookPath))
        {
            throw new BuildFailureException("Asset cooking", 1);
        }

        if (Directory.Exists(_sdkContentModsOurDir))
        {
            if (Directory.GetFiles(_sdkContentModsOurDir, "*", SearchOption.AllDirectories).Any())
            {
                throw new Exception($"{_sdkContentModsOurDir} is already in use (not empty)");
            }
        }

        var shippedGpcdPath = Path.Combine(_project.SdkPath, "XComGame", "CookedPCConsole", "GlobalPersistentCookerData.upk");
        if (!File.Exists(shippedGpcdPath))
        {
            throw new Exception($"{shippedGpcdPath} does not exist. Please verify your SDK is configured correctly");
        }
    }

    /// <summary>
    /// Prepares the local build cache by creating temporary collection maps.
    /// </summary>
    private async Task PrepareProjectCacheAsync()
    {
        if (Directory.Exists(_collectionMapsPath))
        {
            Directory.Delete(_collectionMapsPath, true);
        }
        Directory.CreateDirectory(_collectionMapsPath);

        foreach (var map in _sfCollectionOnlyMaps)
        {
            var destPath = Path.Combine(_collectionMapsPath, $"{map}.umap");
            await ExtractEmptyUMapAsync(destPath);
        }
    }

    /// <summary>
    /// Validates that cached TFC files have not been modified externally.
    /// Triggers a full recook if inconsistencies are detected.
    /// </summary>
    private void VerifyCachedTfcsNotAltered()
    {
        if (!CheckCachedTfcsNotAltered())
        {
            _logger.LogInformation("Performing a full recook");
            CleanModAssetCookerOutput(_project.SdkPath, _project.ModNameCanonical, new[] { _contentForCookPath, _collectionMapsPath });

            _cookerOutputTracker.TfcFiles.Clear();
            RecordCookerOutputTracker();
        }
    }

    /// <summary>
    /// Checks the structural integrity and timestamps of existing TFC files against tracked metadata.
    /// </summary>
    /// <returns>True if all TFC files are consistent with the tracker; otherwise false.</returns>
    private bool CheckCachedTfcsNotAltered()
    {
        var currentTfcs = GetOurTfcFiles().Select(f => f.Name).ToList();

        foreach (var trackedFileData in _cookerOutputTracker.TfcFiles)
        {
            if (!currentTfcs.Contains(trackedFileData.FullFileName))
            {
                _logger.LogInformation("{FileName} is missing", trackedFileData.FullFileName);
                return false;
            }

            var path = Path.Combine(_project.CookerOutputPath, trackedFileData.FullFileName);
            var file = new FileInfo(path);

            if (file.LastWriteTimeUtc.Ticks != trackedFileData.LastUpdatedUtc)
            {
                _logger.LogInformation("{FileName} timestamp mismatch", trackedFileData.FullFileName);
                return false;
            }

            currentTfcs.Remove(trackedFileData.FullFileName);
        }

        if (currentTfcs.Count > 0)
        {
            _logger.LogInformation("Unexpected TFCs found: {Files}", string.Join(", ", currentTfcs));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Synchronizes SF package timestamps and deletes untracked or modified packages to ensure incremental consistency.
    /// </summary>
    private void VerifyCachedSfPackagesNotAltered()
    {
        foreach (var trackedFileData in _cookerOutputTracker.SfPackages)
        {
            var path = Path.Combine(_project.CookerOutputPath, trackedFileData.FullFileName);
            if (File.Exists(path))
            {
                var file = new FileInfo(path);
                if (file.LastWriteTimeUtc.Ticks != trackedFileData.LastUpdatedUtc)
                {
                    _logger.LogInformation("{FileName} timestamp mismatch - deleting", trackedFileData.FullFileName);
                    File.Delete(path);
                }
            }
        }

        foreach (var fileName in GetDesiredOutputPackageFileNames())
        {
            var path = Path.Combine(_project.CookerOutputPath, fileName);
            var trackedFileData = GetSfPackageTrackerData(fileName);

            if (trackedFileData == null && File.Exists(path))
            {
                _logger.LogInformation("{FileName} exists, but is not tracked - deleting", fileName);
                File.Delete(path);
            }
        }
    }

    /// <summary>
    /// Analyzes source content and existing build artifacts to identify which maps require recooking.
    /// </summary>
    private void DetermineDirtyMaps()
    {
        _dirtyMaps = new List<string>();

        foreach (var map in _cookedMaps)
        {
            if (_sfCollectionOnlyMaps.Contains(map)) continue;

            var cookedPath = Path.Combine(_project.CookerOutputPath, $"{map}.upk");

            if (!File.Exists(cookedPath))
            {
                _logger.LogInformation("{Map} has no cooked version", map);
                _dirtyMaps.Add(map);
            }
            else
            {
                var original = Directory.GetFiles(_contentForCookPath, $"{map}.umap", SearchOption.AllDirectories).FirstOrDefault();
                if (original != null)
                {
                    var originalFile = new FileInfo(original);
                    var cookedFile = new FileInfo(cookedPath);
                    if (originalFile.LastWriteTime > cookedFile.LastWriteTime)
                    {
                        _dirtyMaps.Add(map);
                        _logger.LogInformation("{Map} original was updated", map);
                    }
                }
            }
        }

        foreach (var mapDef in _contentOptions.SfCollectionMaps)
        {
            var map = mapDef.Name;
            var cookedPath = Path.Combine(_project.CookerOutputPath, $"{map}.upk");

            if (_dirtyMaps.Contains(map)) continue;

            if (!File.Exists(cookedPath))
            {
                _logger.LogInformation("{Map} has no cooked version", map);
                _dirtyMaps.Add(map);
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
                            _dirtyMaps.Add(map);
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Creates a temporary 'injected' version of the SDK's Engine.ini to make mod assets visible to the cooker.
    /// </summary>
    private void PrepareEngineIni()
    {
        var lines = PrepareEngineIniAdditions();
        var fileNamePrefix = "AssetsCook";
        
        var localDefaultEngineIniPath = Path.Combine(_project.BuildCachePath, $"{fileNamePrefix}_DefaultEngine.ini");
        var localXComEngineIniPath = Path.Combine(_project.BuildCachePath, $"{fileNamePrefix}_XComEngine.ini");

        var sdkEngineIniPath = Path.Combine(_project.SdkPath, "XComGame", "Config", "DefaultEngine.ini");
        var sdkEngineIniContent = File.ReadAllText(sdkEngineIniPath);
        var newEngineIniContent = sdkEngineIniContent + "\n" + string.Join("\n", lines) + "\n";
        
        File.WriteAllText(localDefaultEngineIniPath, newEngineIniContent);
        File.WriteAllText(localXComEngineIniPath, ""); 

        _engineIniDefaultPath = localDefaultEngineIniPath;
        _engineIniXComPath = localXComEngineIniPath;
    }

    /// <summary>
    /// Generates the INI section additions required to redirect the cooker to mod source and cache directories.
    /// </summary>
    /// <returns>An array of formatted INI lines.</returns>
    private string[] PrepareEngineIniAdditions()
    {
        var lines = new List<string>();

        lines.Add("[Core.System]");
        lines.Add($"+Paths={_contentForCookPath}");
        lines.Add("-Paths=..\\..\\XComGame\\Content\\Mods"); 

        if (_sfCollectionOnlyMaps.Count > 0)
        {
            lines.Add($"+Paths={_collectionMapsPath}");
        }

        lines.Add("[Engine.X2DirectoriesToSkipEnumeration]");
        lines.Add(".Directory=..\\..\\XComGame");
        lines.Add(".Directory=..\\..\\Engine");

        lines.Add("[Engine.PackagesToForceCookPerMap]");
        foreach (var mapDef in _contentOptions.SfCollectionMaps)
        {
            lines.Add($"+Map={mapDef.Name}");
            foreach (var package in mapDef.Packages)
            {
                lines.Add($"+Package={package}");
            }
        }

        return lines.ToArray();
    }

    /// <summary>
    /// Ensures necessary SDK and cooker directories exist and are synchronized with base game artifacts.
    /// </summary>
    private async Task PrepareSdkFoldersAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_project.CookerOutputPath))
        {
            _logger.LogInformation("Creating {Path} directory...", _project.CookerOutputPath);
            Directory.CreateDirectory(_project.CookerOutputPath);
        }

        await SyncBaseGameAssetsAsync(ct);

        if (!Directory.Exists(_sdkContentModsOurDir))
        {
            _logger.LogInformation("Creating {Path} directory...", _sdkContentModsOurDir);
            Directory.CreateDirectory(_sdkContentModsOurDir);
        }
    }

    /// <summary>
    /// Synchronizes core game cooking artifacts (GPCD, GuidCache, Shaders) and base TFC files into the SDK cooker output directory.
    /// </summary>
    private async Task SyncBaseGameAssetsAsync(CancellationToken ct)
    {
        var gameCookedPath = Path.Combine(_project.GamePath, "XComGame", "CookedPCConsole");
        string[] baseFiles = { "GuidCache.upk", "GlobalPersistentCookerData.upk", "PersistentCookerShaderData.bin" };

        if (!Directory.Exists(gameCookedPath))
        {
            _logger.LogWarning("Game CookedPCConsole not found at {Path}. Skipping base asset sync.", gameCookedPath);
            return;
        }

        foreach (var file in baseFiles)
        {
            var src = Path.Combine(gameCookedPath, file);
            var dest = Path.Combine(_project.CookerOutputPath, file);
            if (!File.Exists(dest) && File.Exists(src))
            {
                _logger.LogInformation("Copying balance artifact {File}...", file);
                File.Copy(src, dest);
            }
        }

        _logger.LogInformation("Synchronizing base game Texture File Caches (*.tfc)...");
        await _mirror.MirrorWithArgsAsync(gameCookedPath, _project.CookerOutputPath, "*.tfc", "/NJH /XC /XN /XO", ct);
    }

    /// <summary>
    /// Constructs the CLI arguments for the asset cooker commandlet.
    /// </summary>
    private void PrepareEditorArgs()
    {
        var cookerFlags = $"-platform=pcconsole -skipmaps -TFCSUFFIX=_XPACK_ -singlethread -unattended -DLCName={_project.ModNameCanonical}";
        if (_project.FinalRelease)
        {
            cookerFlags += " -final_release";
        }
        var mapsString = string.Join(" ", _dirtyMaps);

        _editorArgs = $"CookPackages {mapsString} {cookerFlags} -DEFENGINEINI=\"{_engineIniDefaultPath}\" -ENGINEINI=\"{_engineIniXComPath}\"";
    }

    /// <summary>
    /// Executes the asset cooking process by invoking the UnrealEd commandlet.
    /// Includes the 'IteratorGuard' hack and cleanup logic to ensure SDK stability.
    /// </summary>
    private async Task ExecuteCoreAsync(CancellationToken ct)
    {
        try
        {
            if (_contentOptions.SfStandalone.Count > 0)
            {
                await CreateMarkerPackageFileAsync("000000000_________IteratorGuard");
                foreach (var package in _contentOptions.SfStandalone)
                {
                    await CreateMarkerPackageFileAsync(package);
                }
            }

            await InvokeAssetCookerAsync(_editorArgs, ct);
        }
        finally
        {
            _logger.LogInformation("Cleaning up the asset cooking hacks");
            var cleanupFailed = false;

            try
            {
                Directory.Delete(_sdkContentModsOurDir, true);
                _logger.LogInformation("Emptied {Path}", _sdkContentModsOurDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to empty {Path}", _sdkContentModsOurDir);
                cleanupFailed = true;
            }

            if (cleanupFailed)
            {
                throw new Exception($"Failed to clean up the asset cooking hacks - your SDK is now in a corrupted state. Please preform the cleanup manually before building a mod or opening the editor.");
            }
        }
    }

    /// <summary>
    /// Creates a dummy package file to satisfy the cooker's iteration requirements.
    /// </summary>
    private async Task CreateMarkerPackageFileAsync(string packageName)
    {
        var path = Path.Combine(_sdkContentModsOurDir, $"{packageName}.upk");
        await File.WriteAllTextAsync(path, "");
    }

    /// <summary>
    /// Invokes the UnrealEd.com commandlet with the prepared editor arguments.
    /// </summary>
    private async Task InvokeAssetCookerAsync(string editorArguments, CancellationToken ct)
    {
        _logger.LogInformation(editorArguments);

        var receiver = new ModcookReceiver(_loggerFactory.CreateLogger<ModcookReceiver>());
        receiver.ProcessDescription = "cooking mod packages";

        var commandletPath = Path.Combine(_project.SdkPath, "binaries", "Win64", "XComGame.com");
        await _runner.RunProcessWithSleepAsync(commandletPath, editorArguments, receiver, 0, 0, ct);
    }

    /// <summary>
    /// Analyzes TFC file growth and logs warnings if significant data duplication is suspected.
    /// </summary>
    private void WarnTfcGrowth()
    {
        var tfcs = GetOurTfcFiles();
        var growthEntries = new List<object>();

        foreach (var file in tfcs)
        {
            var trackedFileData = GetTfcTrackerData(file.Name);
            if (trackedFileData == null) continue; 
            if (file.Length == trackedFileData.OriginalSize) continue;

            var increase = (double)file.Length / trackedFileData.OriginalSize;
            growthEntries.Add(new
            {
                Name = file.Name,
                OriginalSize = BuildUtilities.FormatFileSize(trackedFileData.OriginalSize),
                CurrentSize = BuildUtilities.FormatFileSize(file.Length),
                Increase = $"{increase:F2}x"
            });
        }

        if (growthEntries.Count > 0)
        {
            _logger.LogInformation(TableFormatter.Format(growthEntries));
            _logger.LogInformation("WARNING: TFC files grew since initial creation. This could indicate data duplication.");
            _logger.LogInformation("Your mod will still function normally, but the file size might be larger than needed");
            _logger.LogInformation("You should consider doing a full rebuild before distributing your mod.");
        }
    }

    /// <summary>
    /// Persists the current state of build artifacts (TFC and SF packages) to the tracker file for incremental builds.
    /// </summary>
    private void RecordCookerOutputTracker()
    {
        var tfcs = GetOurTfcFiles();
        foreach (var file in tfcs)
        {
            var trackedFileData = GetTfcTrackerData(file.Name);
            if (trackedFileData == null)
            {
                _cookerOutputTracker.TfcFiles.Add(new TfcFileData
                {
                    FullFileName = file.Name,
                    OriginalSize = file.Length,
                    LastUpdatedUtc = file.LastWriteTimeUtc.Ticks
                });
            }
            else
            {
                trackedFileData.LastUpdatedUtc = file.LastWriteTimeUtc.Ticks;
            }
        }

        var sfPackageFileNames = GetDesiredOutputPackageFileNames();
        _cookerOutputTracker.SfPackages = _cookerOutputTracker.SfPackages
            .Where(sf => sfPackageFileNames.Contains(sf.FullFileName))
            .ToList();

        foreach (var fileName in sfPackageFileNames)
        {
            var filePath = Path.Combine(_project.CookerOutputPath, fileName);
            if (File.Exists(filePath))
            {
                var file = new FileInfo(filePath);
                var trackedFileData = GetSfPackageTrackerData(fileName);
                if (trackedFileData == null)
                {
                    _cookerOutputTracker.SfPackages.Add(new SfPackageData
                    {
                        FullFileName = fileName,
                        LastUpdatedUtc = file.LastWriteTimeUtc.Ticks
                    });
                }
                else
                {
                    trackedFileData.LastUpdatedUtc = file.LastWriteTimeUtc.Ticks;
                }
            }
        }

        var json = System.Text.Json.JsonSerializer.Serialize(_cookerOutputTracker, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_cookerOutputTrackerPath, json);
    }

    /// <summary>
    /// Copies the finalized build artifacts from the cooker output to the project staging directory.
    /// </summary>
    private void StageArtifacts()
    {
        var stagingCookedDir = Path.Combine(_stagingPath, "CookedPCConsole");
        
        if (!Directory.Exists(stagingCookedDir))
        {
            Directory.CreateDirectory(stagingCookedDir);
        }

        foreach (var tfc in GetOurTfcFiles())
        {
            File.Copy(tfc.FullName, Path.Combine(stagingCookedDir, tfc.Name), true);
        }

        foreach (var umap in _cookedMaps)
        {
            var src = Path.Combine(_project.CookerOutputPath, $"{umap}.upk");
            if (File.Exists(src))
            {
                File.Copy(src, Path.Combine(stagingCookedDir, $"{umap}.upk"), true);
            }
        }

        foreach (var package in _contentOptions.SfStandalone)
        {
            var src = Path.Combine(_project.CookerOutputPath, $"{package}_SF.upk");
            var dest = Path.Combine(stagingCookedDir, $"{package}.upk");
            if (File.Exists(src))
            {
                File.Copy(src, dest, true);
            }
        }
    }

    /// <summary>
    /// Retrieves all TFC files generated for the current mod project in the SDK cooker output directory.
    /// </summary>
    /// <returns>An array of <see cref="FileInfo"/> objects for the mod's TFC files.</returns>
    private FileInfo[] GetOurTfcFiles()
    {
        return Directory.GetFiles(_project.CookerOutputPath, $"*{_actualTfcSuffix}.tfc")
            .Select(f => new FileInfo(f))
            .ToArray();
    }

    /// <summary>
    /// Generates the list of filenames for all expected output packages (SF and Maps).
    /// </summary>
    /// <returns>An array of expected package filenames.</returns>
    private string[] GetDesiredOutputPackageFileNames()
    {
        var sfPackageFileNames = _contentOptions.SfStandalone.Select(p => $"{p}_SF.upk").OfType<string>().ToList();
        sfPackageFileNames.AddRange(_cookedMaps.Select(m => $"{m}.upk").OfType<string>());
        return sfPackageFileNames.ToArray();
    }

    /// <summary>
    /// Retrieves tracking metadata for a specific TFC file.
    /// </summary>
    private TfcFileData? GetTfcTrackerData(string fullFileName)
    {
        return _cookerOutputTracker.TfcFiles.FirstOrDefault(f => f.FullFileName == fullFileName);
    }

    /// <summary>
    /// Retrieves tracking metadata for a specific SF package.
    /// </summary>
    private SfPackageData? GetSfPackageTrackerData(string fullFileName)
    {
        return _cookerOutputTracker.SfPackages.FirstOrDefault(f => f.FullFileName == fullFileName);
    }

    /// <summary>
    /// Checks if any asset cooking is requested based on Content options.
    /// </summary>
    private bool AnyAssetsToCook()
    {
        return _contentOptions.SfStandalone.Count > 0 || 
               _contentOptions.SfMaps.Count > 0 || 
               _contentOptions.SfCollectionMaps.Count > 0;
    }

    /// <summary>
    /// Extracts an embedded map resource to a target path.
    /// </summary>
    private async Task ExtractEmptyUMapAsync(string destinationPath)
    {
        var assembly = typeof(ModAssetsCookStep).Assembly;
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
    /// Removes mod-specific build artifacts from the SDK installation.
    /// </summary>
    private void CleanModAssetCookerOutput(string sdkPath, string modNameCanonical, string[] excludePaths)
    {
        var tfcSuffix = $"_{modNameCanonical}_DLCTFC_XPACK_";
        var tfcFiles = Directory.GetFiles(Path.Combine(sdkPath, "XComGame", "Published", "CookedPCConsole"), $"*{tfcSuffix}.tfc");
        foreach (var tfc in tfcFiles)
        {
            File.Delete(tfc);
        }

        var sfPackages = _contentOptions.SfStandalone.Select(p => $"{p}_SF.upk").Concat(_cookedMaps.Select(m => $"{m}.upk"));
        foreach (var pkg in sfPackages)
        {
            var pkgPath = Path.Combine(sdkPath, "XComGame", "Published", "CookedPCConsole", pkg);
            if (File.Exists(pkgPath))
            {
                File.Delete(pkgPath);
            }
        }
    }
}
