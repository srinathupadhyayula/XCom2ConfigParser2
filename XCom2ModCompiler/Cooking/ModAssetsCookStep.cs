using System.Text;
using Microsoft.Extensions.Logging;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Compilation;
using XCom2ModCompiler.Utilities;
using XCom2ModCompiler.Tracking;
using XCom2ModCompiler.Exceptions;

namespace XCom2ModCompiler.Cooking;


/// <summary>
/// Asset cooking step - mirrors PowerShell ModAssetsCookStep class.
/// Handles the complete asset cooking pipeline with incremental support.
/// </summary>
public class ModAssetsCookStep
{
    private readonly BuildOptions _project;
    private readonly ContentOptions _contentOptions;
    private readonly IProcessRunner _runner;
    private readonly IFileMirror _mirror;
    private readonly BuildTracker _tracker;
    private readonly string _stagingPath;
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

    public ModAssetsCookStep(
        BuildOptions project,
        ContentOptions contentOptions,
        string stagingPath,
        IProcessRunner runner,
        IFileMirror mirror,
        BuildTracker tracker,
        ILogger<ModAssetsCookStep> logger)
    {
        _project = project;
        _contentOptions = contentOptions;
        _stagingPath = stagingPath;
        _runner = runner;
        _mirror = mirror;
        _tracker = tracker;
        _logger = logger;
    }

    /// <summary>
    /// Executes the complete asset cooking pipeline.
    /// </summary>
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

    private void Init()
    {
        _actualTfcSuffix = $"_{_project.ModNameCanonical}_DLCTFC_XPACK_";
        _contentForCookPath = Path.Combine(_project.ModSrcRoot, "ContentForCook");
        _collectionMapsPath = Path.Combine(_project.BuildCachePath, "CollectionMaps");
        _sdkContentModsDir = Path.Combine(_project.SdkPath, "XComGame", "Content", "Mods");
        _sdkContentModsOurDir = Path.Combine(_sdkContentModsDir, _project.ModNameCanonical);

        // Build list of maps to cook
        _cookedMaps = new List<string>(_contentOptions.SfMaps);
        foreach (var mapDef in _contentOptions.SfCollectionMaps)
        {
            _cookedMaps.Add(mapDef.Name);
        }

        // Find collection-only maps (maps that don't exist in ContentForCook)
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

        // Load cooker output tracker
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

        // Verify shipped GPCD exists
        var shippedGpcdPath = Path.Combine(_project.SdkPath, "XComGame", "CookedPCConsole", "GlobalPersistentCookerData.upk");
        if (!File.Exists(shippedGpcdPath))
        {
            throw new Exception($"{shippedGpcdPath} does not exist. Please verify your SDK is configured correctly");
        }
    }

    private async Task PrepareProjectCacheAsync()
    {
        // Prep the folder for collection maps
        if (Directory.Exists(_collectionMapsPath))
        {
            Directory.Delete(_collectionMapsPath, true);
        }
        Directory.CreateDirectory(_collectionMapsPath);

        // Create empty collection maps for collection-only maps
        foreach (var map in _sfCollectionOnlyMaps)
        {
            var destPath = Path.Combine(_collectionMapsPath, $"{map}.umap");
            await ExtractEmptyUMapAsync(destPath);
        }
    }

    private void VerifyCachedTfcsNotAltered()
    {
        if (!CheckCachedTfcsNotAltered())
        {
            _logger.LogInformation("Performing a full recook");
            CleanModAssetCookerOutput(_project.SdkPath, _project.ModNameCanonical, new[] { _contentForCookPath, _collectionMapsPath });

            // Save that everything is deleted
            _cookerOutputTracker.TfcFiles.Clear();
            RecordCookerOutputTracker();
        }
    }

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

    private void VerifyCachedSfPackagesNotAltered()
    {
        // Delete tracked if timestamp doesn't match
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

        // Delete supposed-to-cook if they exist but are not tracked
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

    private void DetermineDirtyMaps()
    {
        _dirtyMaps = new List<string>();

        // Check the dev-made maps
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

        // Check the collection maps
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

    private void PrepareEngineIni()
    {
        var lines = PrepareEngineIniAdditions();
        var fileNamePrefix = "AssetsCook";
        
        var localDefaultEngineIniPath = Path.Combine(_project.BuildCachePath, $"{fileNamePrefix}_DefaultEngine.ini");
        var localXComEngineIniPath = Path.Combine(_project.BuildCachePath, $"{fileNamePrefix}_XComEngine.ini");

        // Read SDK Engine.ini and add our additions
        var sdkEngineIniPath = Path.Combine(_project.SdkPath, "XComGame", "Config", "DefaultEngine.ini");
        var sdkEngineIniContent = File.ReadAllText(sdkEngineIniPath);
        var newEngineIniContent = sdkEngineIniContent + "\n" + string.Join("\n", lines) + "\n";
        
        File.WriteAllText(localDefaultEngineIniPath, newEngineIniContent);
        File.WriteAllText(localXComEngineIniPath, ""); // Empty XComEngine.ini

        _engineIniDefaultPath = localDefaultEngineIniPath;
        _engineIniXComPath = localXComEngineIniPath;
    }

    private string[] PrepareEngineIniAdditions()
    {
        var lines = new List<string>();

        // "Inject" our assets into the SDK to make them visible to the cooker
        lines.Add("[Core.System]");
        lines.Add($"+Paths={_contentForCookPath}");
        lines.Add("-Paths=..\\..\\XComGame\\Content\\Mods"); // Do not actually load the packages from there

        if (_sfCollectionOnlyMaps.Count > 0)
        {
            lines.Add($"+Paths={_collectionMapsPath}");
        }

        // Stop all the "Adding [...]" garbage
        lines.Add("[Engine.X2DirectoriesToSkipEnumeration]");
        lines.Add(".Directory=..\\..\\XComGame");
        lines.Add(".Directory=..\\..\\Engine");

        // Collection maps
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

    private async Task PrepareSdkFoldersAsync(CancellationToken ct)
    {
        // Ensure cooker output directory exists
        if (!Directory.Exists(_project.CookerOutputPath))
        {
            _logger.LogInformation("Creating {Path} directory...", _project.CookerOutputPath);
            Directory.CreateDirectory(_project.CookerOutputPath);
        }

        // Parity: Copy base game artifacts if missing
        await SyncBaseGameAssetsAsync(ct);

        // Create our Content Mods directory
        if (!Directory.Exists(_sdkContentModsOurDir))
        {
            _logger.LogInformation("Creating {Path} directory...", _sdkContentModsOurDir);
            Directory.CreateDirectory(_sdkContentModsOurDir);
        }
    }

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
        // robocopy /NJH /XC /XN /XO (Parity with PS1)
        await _mirror.MirrorWithArgsAsync(gameCookedPath, _project.CookerOutputPath, "*.tfc", "/NJH /XC /XN /XO", ct);
    }

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

    private async Task ExecuteCoreAsync(CancellationToken ct)
    {
        try
        {
            // Create iterator guard and dummy files for SF standalone packages
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

    private async Task CreateMarkerPackageFileAsync(string packageName)
    {
        var path = Path.Combine(_sdkContentModsOurDir, $"{packageName}.upk");
        await File.WriteAllTextAsync(path, "");
    }

    private async Task InvokeAssetCookerAsync(string editorArguments, CancellationToken ct)
    {
        _logger.LogInformation(editorArguments);

        var receiver = new ModcookReceiver();
        receiver.ProcessDescription = "cooking mod packages";

        var commandletPath = Path.Combine(_project.SdkPath, "binaries", "Win64", "XComGame.com");
        await _runner.RunProcessWithSleepAsync(commandletPath, editorArguments, receiver, 0, 0, ct);
    }

    private void WarnTfcGrowth()
    {
        var tfcs = GetOurTfcFiles();
        var growthEntries = new List<object>();

        foreach (var file in tfcs)
        {
            var trackedFileData = GetTfcTrackerData(file.Name);
            if (trackedFileData == null) continue; // New file - ignore
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

    private void RecordCookerOutputTracker()
    {
        // TFCs
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

        // SF packages
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

        // Write the tracker file
        var json = System.Text.Json.JsonSerializer.Serialize(_cookerOutputTracker, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_cookerOutputTrackerPath, json);
    }

    private void StageArtifacts()
    {
        // Prepare the folder for cooked stuff
        var stagingCookedDir = Path.Combine(_stagingPath, "CookedPCConsole");
        
        if (!Directory.Exists(stagingCookedDir))
        {
            Directory.CreateDirectory(stagingCookedDir);
        }

        // Copy over the TFC files
        foreach (var tfc in GetOurTfcFiles())
        {
            File.Copy(tfc.FullName, Path.Combine(stagingCookedDir, tfc.Name), true);
        }

        // Copy over the maps
        foreach (var umap in _cookedMaps)
        {
            var src = Path.Combine(_project.CookerOutputPath, $"{umap}.upk");
            if (File.Exists(src))
            {
                File.Copy(src, Path.Combine(stagingCookedDir, $"{umap}.upk"), true);
            }
        }

        // Copy over the SF packages
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

    private FileInfo[] GetOurTfcFiles()
    {
        return Directory.GetFiles(_project.CookerOutputPath, $"*{_actualTfcSuffix}.tfc")
            .Select(f => new FileInfo(f))
            .ToArray();
    }

    private string[] GetDesiredOutputPackageFileNames()
    {
        var sfPackageFileNames = _contentOptions.SfStandalone.Select(p => $"{p}_SF.upk").OfType<string>().ToList();
        sfPackageFileNames.AddRange(_cookedMaps.Select(m => $"{m}.upk").OfType<string>());
        return sfPackageFileNames.ToArray();
    }

    private TfcFileData? GetTfcTrackerData(string fullFileName)
    {
        return _cookerOutputTracker.TfcFiles.FirstOrDefault(f => f.FullFileName == fullFileName);
    }

    private SfPackageData? GetSfPackageTrackerData(string fullFileName)
    {
        return _cookerOutputTracker.SfPackages.FirstOrDefault(f => f.FullFileName == fullFileName);
    }

    private bool AnyAssetsToCook()
    {
        return _contentOptions.SfStandalone.Count > 0 || 
               _contentOptions.SfMaps.Count > 0 || 
               _contentOptions.SfCollectionMaps.Count > 0;
    }

    private async Task ExtractEmptyUMapAsync(string destinationPath)
    {
        var assembly = typeof(ModAssetsCookStep).Assembly;
        var resourceName = "XCom2ModCompiler.Resources.EmptyUMap";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new Exception($"Embedded resource {resourceName} not found.");
        }

        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);
    }

    private void CleanModAssetCookerOutput(string sdkPath, string modNameCanonical, string[] excludePaths)
    {
        // Delete TFC files
        var tfcSuffix = $"_{modNameCanonical}_DLCTFC_XPACK_";
        var tfcFiles = Directory.GetFiles(Path.Combine(sdkPath, "XComGame", "Published", "CookedPCConsole"), $"*{tfcSuffix}.tfc");
        foreach (var tfc in tfcFiles)
        {
            File.Delete(tfc);
        }

        // Delete SF packages
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
