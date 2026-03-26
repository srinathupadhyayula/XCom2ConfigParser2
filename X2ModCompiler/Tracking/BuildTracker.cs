using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;
using ZLogger;

namespace X2ModCompiler.Tracking;

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
        if (fingerprint == null)
        {
            return new BuildFingerprint("", "", DateTime.MinValue, DateTime.MinValue, new Dictionary<string, DateTime>(), new Dictionary<string, DateTime>());
        }
        
        // Handle old fingerprint files that don't have ModPackageTimestamps
        if (fingerprint.ModPackageTimestamps == null)
        {
            fingerprint = fingerprint with { ModPackageTimestamps = new Dictionary<string, DateTime>() };
        }
        
        // Handle old fingerprint files that don't have FileTimestamps
        if (fingerprint.FileTimestamps == null)
        {
            fingerprint = fingerprint with { FileTimestamps = new Dictionary<string, DateTime>() };
        }
        
        return fingerprint;
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
            _logger.ZLogInformation($"Detected switch between debug and release build.");
            return true;
        }

        if (fingerprint.GlobalsHash != globalsHash)
        {
            _logger.ZLogInformation($"Detected change in macros (Globals.uci).");
            return true;
        }

        return false;
    }

    public virtual async Task<bool> HasCorePackageChangedAsync(DateTime coreTimestamp, CancellationToken ct = default)
    {
        var fingerprint = await LoadFingerprintAsync(ct);
        if (fingerprint.CoreTimestamp != coreTimestamp)
        {
            _logger.ZLogInformation($"Detected external rebuild of Core packages.");
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
            _logger.ZLogInformation($"First build - full rebuild required.");
            return true;
        }

        // Check build mode switch
        var currentMode = currentOptions.Debug ? "debug" : "release";
        if (fingerprint.BuildMode != currentMode)
        {
            _logger.ZLogInformation($"Detected switch between debug and release build.");
            return true;
        }

        // Check Globals.uci hash
        if (fingerprint.GlobalsHash != globalsHash)
        {
            _logger.ZLogInformation($"Detected change in macros (Globals.uci).");
            return true;
        }

        // Check Core.u timestamp if provided
        if (coreTimestamp.HasValue && fingerprint.CoreTimestamp != coreTimestamp.Value)
        {
            _logger.ZLogInformation($"Detected external rebuild of Core packages.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if any mod script packages have been modified since their .u files were last generated.
    /// This is used to determine if cleanup is needed before compilation.
    /// Mimics the timestamp checking logic from PowerShell _CheckCleanCompiled() but for mod packages only.
    /// </summary>
    /// <param name="sdkPath">The SDK path where .u files are stored.</param>
    /// <param name="modSrcPath">The mod source path containing .uc files.</param>
    /// <param name="modScriptPackages">List of mod script package names.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if any mod package source has been modified since the last build.</returns>
    public virtual async Task<bool> ShouldCleanModScriptsAsync(string sdkPath, string modSrcPath, string[] modScriptPackages, CancellationToken ct = default)
    {
        var fingerprint = await LoadFingerprintAsync(ct);

        // Check each mod package source file against its compiled .u timestamp
        foreach (var packageName in modScriptPackages)
        {
            var sourceFile = Path.Combine(modSrcPath, packageName, $"{packageName}.uc");
            var compiledFile = Path.Combine(sdkPath, "XComGame", "Script", $"{packageName}.u");

            // If source exists but compiled doesn't, we need to compile (but not necessarily clean)
            if (File.Exists(sourceFile) && !File.Exists(compiledFile))
            {
                _logger.ZLogInformation($"Package {packageName}: source exists but no compiled .u found.");
                return false; // No cleanup needed, compiler will create new
            }

            // If both exist, check timestamps
            if (File.Exists(sourceFile) && File.Exists(compiledFile))
            {
                var sourceTime = File.GetLastWriteTime(sourceFile);
                var compiledTime = File.GetLastWriteTime(compiledFile);

                if (sourceTime > compiledTime)
                {
                    _logger.ZLogInformation($"Package {packageName}: source modified ({sourceTime}) after compile ({compiledTime}).");
                    return true; // Cleanup needed
                }
            }
        }

        // Also check ModPackageTimestamps from fingerprint for any tracked source changes
        foreach (var kvp in fingerprint.ModPackageTimestamps)
        {
            var packageName = kvp.Key;
            var recordedTime = kvp.Value;
            var sourceFile = Path.Combine(modSrcPath, packageName, $"{packageName}.uc");

            if (File.Exists(sourceFile))
            {
                var currentTime = File.GetLastWriteTime(sourceFile);
                if (currentTime > recordedTime)
                {
                    _logger.ZLogInformation($"Package {packageName}: source modified since last build.");
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Records the timestamps of mod source files after successful compilation.
    /// </summary>
    /// <param name="modSrcPath">The mod source path.</param>
    /// <param name="modScriptPackages">List of mod script package names.</param>
    /// <param name="ct">Cancellation token.</param>
    public virtual async Task RecordModTimestampsAsync(string modSrcPath, string[] modScriptPackages, CancellationToken ct = default)
    {
        var fingerprint = await LoadFingerprintAsync(ct);
        var modPackageTimestamps = new Dictionary<string, DateTime>();

        foreach (var packageName in modScriptPackages)
        {
            var sourceFile = Path.Combine(modSrcPath, packageName, $"{packageName}.uc");
            if (File.Exists(sourceFile))
            {
                modPackageTimestamps[packageName] = File.GetLastWriteTime(sourceFile);
            }
        }

        fingerprint = fingerprint with { ModPackageTimestamps = modPackageTimestamps, LastBuildTime = DateTime.UtcNow };
        await SaveFingerprintAsync(fingerprint, ct);
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
    /// Computes an XxHash3 hash of the specified file for high-performance change detection.
    /// </summary>
    /// <param name="filePath">The absolute path to the file.</param>
    /// <returns>The hexadecimal string representation of the hash, or an empty string if the file does not exist.</returns>
    public static string ComputeFileHash(string filePath)
    {
        if (!File.Exists(filePath)) return "";
        using var stream = File.OpenRead(filePath);
        var hasher = new XxHash3();
        hasher.Append(stream);
        return Convert.ToHexStringLower(hasher.GetCurrentHash());
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
/// <param name="ModPackageTimestamps">A dictionary mapping mod package names to their source file timestamps for incremental build detection.</param>
public record BuildFingerprint(
    string BuildMode,
    string GlobalsHash,
    DateTime CoreTimestamp,
    DateTime LastBuildTime,
    Dictionary<string, DateTime> FileTimestamps,
    Dictionary<string, DateTime> ModPackageTimestamps);
