using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Utilities;

namespace XCom2ModCompiler.Tracking;

/// <summary>
/// Tracks the state of the build environment to enable incremental builds.
/// This class handles saving and loading "fingerprints" (hashes and timestamps)
/// for core game files and mod sources to detect when a full or selective rebuild is required.
/// </summary>
public class BuildTracker
{
    private readonly string _cachePath;
    private readonly ILogger<BuildTracker> _logger;
    private readonly string _fingerprintFile;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildTracker"/> class.
    /// </summary>
    /// <param name="cachePath">The directory path where build metadata is stored.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public BuildTracker(string cachePath, ILogger<BuildTracker> logger)
    {
        _cachePath = cachePath;
        _logger = logger;
        _fingerprintFile = Path.Combine(_cachePath, "lastBuildDetails.json");
    }

    /// <summary>
    /// Asynchronously loads the build fingerprint from the cache file.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The loaded <see cref="BuildFingerprint"/>, or an empty fingerprint if none exists.</returns>
    public virtual async Task<BuildFingerprint> LoadFingerprintAsync(CancellationToken ct = default)
    {
        var fingerprint = await JsonUtilities.LoadAsync<BuildFingerprint>(_fingerprintFile, ct);
        return fingerprint ?? new BuildFingerprint("", "", DateTime.MinValue, DateTime.MinValue, new Dictionary<string, DateTime>());
    }

    /// <summary>
    /// Asynchronously saves the provided build fingerprint to the cache file.
    /// </summary>
    /// <param name="fingerprint">The fingerprint to save.</param>
    /// <param name="ct">The cancellation token.</param>
    public virtual async Task SaveFingerprintAsync(BuildFingerprint fingerprint, CancellationToken ct = default)
    {
        await JsonUtilities.SaveAsync(_fingerprintFile, fingerprint, ct);
    }

    public virtual async Task<bool> HasConfigurationChangedAsync(BuildOptions currentOptions, string globalsHash, CancellationToken ct = default)
    {
        var fingerprint = await LoadFingerprintAsync(ct);
        
        var currentMode = currentOptions.Debug ? "debug" : "release";
        if (fingerprint.BuildMode != currentMode)
        {
            _logger.LogInformation("Detected switch between debug and release build.");
            return true;
        }

        if (fingerprint.GlobalsHash != globalsHash)
        {
            _logger.LogInformation("Detected change in macros (Globals.uci).");
            return true;
        }

        return false;
    }

    public virtual async Task<bool> HasCorePackageChangedAsync(DateTime coreTimestamp, CancellationToken ct = default)
    {
        var fingerprint = await LoadFingerprintAsync(ct);
        if (fingerprint.CoreTimestamp != coreTimestamp)
        {
            _logger.LogInformation("Detected external rebuild of Core packages.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a rebuild is needed based on build mode, globals hash, and core timestamp.
    /// Mimics PowerShell _CheckCleanCompiled() method.
    /// </summary>
    public virtual async Task<bool> ShouldRebuildAsync(BuildOptions currentOptions, string globalsHash, DateTime? coreTimestamp = null, CancellationToken ct = default)
    {
        var fingerprint = await LoadFingerprintAsync(ct);
        
        // First build - always rebuild
        if (string.IsNullOrEmpty(fingerprint.BuildMode))
        {
            _logger.LogInformation("First build - full rebuild required.");
            return true;
        }

        // Check build mode switch
        var currentMode = currentOptions.Debug ? "debug" : "release";
        if (fingerprint.BuildMode != currentMode)
        {
            _logger.LogInformation("Detected switch between debug and release build.");
            return true;
        }

        // Check Globals.uci hash
        if (fingerprint.GlobalsHash != globalsHash)
        {
            _logger.LogInformation("Detected change in macros (Globals.uci).");
            return true;
        }

        // Check Core.u timestamp if provided
        if (coreTimestamp.HasValue && fingerprint.CoreTimestamp != coreTimestamp.Value)
        {
            _logger.LogInformation("Detected external rebuild of Core packages.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the paths of script files that should be cleaned for selective rebuild.
    /// </summary>
    public virtual async Task<List<string>> GetSelectiveCleanPathsAsync(string sdkPath, string[] modScriptPackages, CancellationToken ct = default)
    {
        var pathsToClean = new List<string>();
        
        var scriptPaths = new[]
        {
            Path.Combine(sdkPath, "XComGame", "Script"),
            Path.Combine(sdkPath, "XComGame", "ScriptFinalRelease")
        };

        foreach (var name in modScriptPackages)
        {
            foreach (var basePath in scriptPaths)
            {
                var scriptPath = Path.Combine(basePath, $"{name}.u");
                if (File.Exists(scriptPath))
                {
                    pathsToClean.Add(scriptPath);
                }
            }
        }

        return pathsToClean;
    }

    /// <summary>
    /// Records the Core.u timestamp after successful compilation.
    /// Mimics PowerShell _RecordCoreTimestamp() method.
    /// </summary>
    public virtual async Task RecordCoreTimestampAsync(string sdkPath, CancellationToken ct = default)
    {
        var fingerprint = await LoadFingerprintAsync(ct);
        var corePath = Path.Combine(sdkPath, "XComGame", "Script", "Core.u");
        
        if (File.Exists(corePath))
        {
            var coreTime = File.GetLastWriteTime(corePath);
            fingerprint = fingerprint with { CoreTimestamp = coreTime };
            await SaveFingerprintAsync(fingerprint, ct);
        }
    }

    /// <summary>
    /// Computes an MD5 hash of the specified file.
    /// </summary>
    /// <param name="filePath">The absolute path to the file.</param>
    /// <returns>The hexadecimal string representation of the MD5 hash, or an empty string if the file does not exist.</returns>
    public static string ComputeFileHash(string filePath)
    {
        if (!File.Exists(filePath)) return "";
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}

/// <summary>
/// Represents a snapshot of the build environment state, used for change detection.
/// </summary>
/// <param name="BuildMode">The build configuration (e.g., "debug" or "release").</param>
/// <param name="GlobalsHash">The MD5 hash of the SDK's Globals.uci file.</param>
/// <param name="CoreTimestamp">The last modified timestamp of the SDK's Core.u binary.</param>
/// <param name="LastBuildTime">The UTC time when the build was last executed.</param>
/// <param name="FileTimestamps">A dictionary mapping source file paths to their last modified timestamps.</param>
public record BuildFingerprint(
    string BuildMode, 
    string GlobalsHash, 
    DateTime CoreTimestamp, 
    DateTime LastBuildTime,
    Dictionary<string, DateTime> FileTimestamps);
