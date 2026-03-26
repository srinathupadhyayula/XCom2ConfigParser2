using System.ComponentModel.DataAnnotations;

namespace X2ModCompiler.Configuration;

/// <summary>
/// Contains mod-specific configuration options.
/// </summary>
public sealed class ModOptions
{
    /// <summary>
    /// Gets or sets the display name of the mod.
    /// </summary>
    [Required]
    public string ModName { get; set; } = "";

    /// <summary>
    /// Gets or sets the Steam Workshop ID for the mod. Set to -1 for local-only mods.
    /// </summary>
    public long WorkshopId { get; set; } = -1;

    /// <summary>
    /// Gets a list of dependent packages for the mod (used for Two-Pass compilation).
    /// </summary>
    public List<string> DependentPackages { get; } = new();

    /// <summary>
    /// Gets a list of mod names that should be cleaned (binaries removed) before building.
    /// </summary>
    public List<string> CleanMods { get; } = new();

    /// <summary>
    /// Gets the canonicalized mod name (whitespace and semicolons removed).
    /// </summary>
    public string ModNameCanonical => ModName.Replace(" ", "").Replace(";", "");
}

/// <summary>
/// Contains path-related configuration options.
/// </summary>
public sealed class PathOptions
{
    /// <summary>
    /// Gets or sets the absolute path to the root directory containing the mod's source code and configuration.
    /// </summary>
    [Required]
    public string ProjectRoot { get; set; } = "";

    /// <summary>
    /// Gets or sets the absolute path to the XCOM 2 SDK installation.
    /// </summary>
    [Required]
    public string SdkPath { get; set; } = "";

    /// <summary>
    /// Gets or sets the absolute path to the XCOM 2 game installation.
    /// </summary>
    [Required]
    public string GamePath { get; set; } = "";

    /// <summary>
    /// Gets or sets the destination directory where the built mod artifacts should be deployed.
    /// </summary>
    [Required]
    public string ModDestinationPath { get; set; } = "";

    /// <summary>
    /// Gets or sets an optional override path for the build cache (fingerprints, trackers, etc.).
    /// </summary>
    public string? BuildCachePathOverride { get; set; }

    /// <summary>
    /// Gets a list of additional directory paths to include during compilation.
    /// </summary>
    public List<string> IncludePaths { get; } = new();

    /// <summary>
    /// Gets the list of directory paths searched for configuration files (extracted from settings.json).
    /// </summary>
    public List<string> IniRoots { get; } = new();

    #region Computed Paths

    /// <summary>
    /// Gets the absolute path to the mod's source root within the project.
    /// </summary>
    public string ModSrcRoot(string modNameCanonical) => Path.Combine(ProjectRoot, modNameCanonical);

    /// <summary>
    /// Gets the absolute path to the mod's staging directory within the SDK.
    /// </summary>
    public string StagingPath(string sdkPath, string modNameCanonical) => Path.Combine(sdkPath, "XComGame", "Mods", modNameCanonical);

    /// <summary>
    /// Gets the absolute path where the final mod binaries and artifacts are placed.
    /// </summary>
    public string FinalModPath(string modDestinationPath, string modNameCanonical) => Path.Combine(modDestinationPath, modNameCanonical);

    /// <summary>
    /// Gets the absolute path to the SDK's internal cooker output directory.
    /// </summary>
    public string CookerOutputPath(string sdkPath) => Path.Combine(sdkPath, "XComGame", "Published", "CookedPCConsole");

    /// <summary>
    /// Gets the absolute path to the active build cache directory.
    /// </summary>
    public string BuildCachePath(string projectRoot) => BuildCachePathOverride ?? Path.Combine(projectRoot, "BuildCache");

    /// <summary>
    /// Gets the absolute path to the UnrealEd 'XComGame.com' commandlet executable.
    /// </summary>
    public string CommandletPath(string sdkPath) => Path.Combine(sdkPath, "binaries", "Win64", "XComGame.com");

    #endregion
}

/// <summary>
/// Contains build flag options.
/// </summary>
public sealed class BuildFlags
{
    /// <summary>
    /// Gets or sets a value indicating whether the build should include debug information.
    /// </summary>
    public bool Debug { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a final release build (enables optimizations and strips debug symbols).
    /// </summary>
    public bool FinalRelease { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to only compile scripts (skipping cooking and deployment).
    /// </summary>
    public bool CompileOnly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use the Two-Pass linkage recovery compilation flow.
    /// </summary>
    public bool TwoPassCompilation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether configuration validation should be performed before building.
    /// </summary>
    public bool ValidateConfig { get; set; }
}
