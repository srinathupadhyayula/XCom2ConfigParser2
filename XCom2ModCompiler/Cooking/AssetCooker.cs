using Microsoft.Extensions.Logging;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Compilation;
using XCom2ModCompiler.Tracking;
using XCom2ModCompiler.Utilities;
using System.Reflection;
using XCom2ModCompiler.Exceptions;

namespace XCom2ModCompiler.Cooking;

/// <summary>
/// Orchestrates the asset cooking process for XCOM 2 mod projects.
/// This class validates the source content and SDK environment before delegating the heavy lifting
/// to the <see cref="ModAssetsCookStep"/> pipeline to mirror official build behavior.
/// </summary>
public class AssetCooker
{
    private readonly string _sdkPath;
    private readonly string _gamePath;
    private readonly string _buildCachePath;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AssetCooker> _logger;
    private readonly IProcessRunner _runner;
    private readonly IFileMirrorParity _mirror;
    private readonly BuildTracker _tracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetCooker"/> class.
    /// </summary>
    /// <param name="sdkPath">The absolute path to the XCOM 2 SDK root.</param>
    /// <param name="gamePath">The absolute path to the XCOM 2 game installation root.</param>
    /// <param name="buildCachePath">The path to the local build cache for tracking fingerprints.</param>
    /// <param name="runner">The process runner for executing UnrealEd commandlets.</param>
    /// <param name="mirror"> The file mirroring service for resource management.</param>
    /// <param name="tracker">The build tracker for maintaining change metadata.</param>
    /// <param name="loggerFactory">The logger factory for creating component-specific loggers.</param>
    /// <param name="logger">The logger for general asset cooking diagnostics.</param>
    public AssetCooker(
        string sdkPath,
        string gamePath,
        string buildCachePath,
        IProcessRunner runner,
        IFileMirrorParity mirror,
        BuildTracker tracker,
        ILoggerFactory loggerFactory,
        ILogger<AssetCooker> logger)
    {
        _sdkPath = sdkPath;
        _gamePath = gamePath;
        _buildCachePath = buildCachePath;
        _loggerFactory = loggerFactory;
        _runner = runner;
        _mirror = mirror;
        _tracker = tracker;
        _logger = logger;
    }

    /// <summary>
    /// Performs asynchronous asset cooking for the specified mod.
    /// Validates required directories and SDK state before executing the cooking pipeline.
    /// </summary>
    /// <param name="modName">The canonical name of the mod project.</param>
    /// <param name="stagingPath">The path where cooked assets should be staged.</param>
    /// <param name="contentOptions">The content-specific options for asset processing.</param>
    /// <param name="buildOptions">The global build options for the project.</param>
    /// <param name="ct">A cancellation token to abort the operation.</param>
    /// <returns>A task representing the asynchronous operation, returning true if cooking succeeded.</returns>
    /// <exception cref="BuildFailureException">Thrown if the SDK environment is invalid or cooking fails.</exception>
    public virtual async Task<bool> CookAsync(
        string modName,
        string stagingPath,
        ContentOptions contentOptions,
        BuildOptions buildOptions,
        CancellationToken ct)
    {
        _logger.LogInformation($"Cooking assets for {modName}...");

        var contentForCookPath = Path.Combine(buildOptions.ModSrcRoot, "ContentForCook");

        // Validate ContentForCook exists when cooking is requested
        if (!Directory.Exists(contentForCookPath))
        {
            _logger.LogWarning($"ContentForCook directory not found at {contentForCookPath}. Skipping cooking.");
            return true;
        }

        // Validate SDK ContentMods directory is not in use
        var sdkContentModsOurDir = Path.Combine(buildOptions.SdkPath, "XComGame", "Content", "Mods", buildOptions.ModNameCanonical);
        if (Directory.Exists(sdkContentModsOurDir))
        {
            if (Directory.GetFiles(sdkContentModsOurDir, "*", SearchOption.AllDirectories).Length > 0)
            {
                throw new BuildFailureException("Asset cooking", 1);
            }
        }

        // Validate shipped GPCD exists
        var shippedGpcdPath = Path.Combine(buildOptions.SdkPath, "XComGame", "CookedPCConsole", "GlobalPersistentCookerData.upk");
        if (!File.Exists(shippedGpcdPath))
        {
            throw new BuildFailureException("Asset cooking", 1);
        }

        // Use the complete ModAssetsCookStep pipeline
        var cookStep = new ModAssetsCookStep(buildOptions, contentOptions, stagingPath, _runner, _mirror, _tracker, _loggerFactory.CreateLogger<ModAssetsCookStep>());
        return await cookStep.ExecuteAsync(ct);
    }

    /// <summary>
    /// Checks if the mod's source content has changed since the last build using file fingerprints.
    /// </summary>
    /// <param name="modName">The name of the mod.</param>
    /// <param name="contentForCookPath">The path to the source content folder.</param>
    /// <param name="buildOptions">The build options.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if changes are detected; otherwise false.</returns>
    private async Task<bool> IsDirtyAsync(string modName, string contentForCookPath, BuildOptions buildOptions, CancellationToken ct)
    {
        var lastFingerprint = await _tracker.LoadFingerprintAsync(ct);
        if (lastFingerprint == null) return true;

        // Check ContentForCook files
        var currentFiles = Directory.GetFiles(contentForCookPath, "*.*", SearchOption.AllDirectories)
            .Select(f => new { Path = f, Info = new FileInfo(f) })
            .ToDictionary(k => k.Path, v => v.Info.LastWriteTimeUtc);

        foreach (var file in currentFiles)
        {
            if (!lastFingerprint.FileTimestamps.TryGetValue(file.Key, out var lastTime) || file.Value > lastTime)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts an embedded 'EmptyMap' resource to a target path.
    /// This map is typically used to satisfy UnrealEd's requirements for a clean cooking environment.
    /// </summary>
    /// <param name="destinationPath">The path where the empty map should be extracted.</param>
    private async Task ExtractEmptyUMapAsync(string destinationPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "XCom2ModCompiler.Resources.EmptyUMap";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new Exception($"Embedded resource {resourceName} not found.");
        }

        using FileStream fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);
    }

    /// <summary>
    /// Cleans up cooked asset artifacts from the SDK and staging directories.
    /// </summary>
    /// <param name="modName">The name of the mod to clean.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual async Task CleanAsync(string modName, CancellationToken ct)
    {
        _logger.LogInformation($"Cleaning cooked assets for {modName}...");
        var publishedPath = Path.Combine(_sdkPath, "XComGame", "Published", "CookedPCConsole");
        // TODO: Implement actual cleanup logic as per clean_cooker_output.ps1
        await Task.CompletedTask;
    }
}
