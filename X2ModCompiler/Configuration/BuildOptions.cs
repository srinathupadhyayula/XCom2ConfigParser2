using X2ModCompiler.Core.Configuration;
using System.ComponentModel.DataAnnotations;

namespace X2ModCompiler.Configuration;

/// <summary>
/// Encapsulates all global build configuration options and computed paths for the X2ModCompiler.
/// This class serves as the central configuration object passed across the build pipeline components.
/// </summary>
public class BuildOptions
{
    /// <summary>
    /// Gets the underlying parser settings used by the Core library.
    /// </summary>
    public ParserSettings ParserSettings { get; } = new();

    /// <summary>
    /// Gets or sets the display name of the mod.
    /// </summary>
    [Required]
    public string ModName { get; init; } = "";
    
    [Required]
    public string ProjectRoot { get; init; } = "";
    
    [Required]
    public string SdkPath { get; init; } = "";
    
    [Required]
    public string GamePath { get; init; } = "";
    
    [Required]
    public string ModDestinationPath { get; init; } = "";
    
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

    /// <summary>
    /// Gets or sets the Steam Workshop ID for the mod. Set to -1 for local-only mods.
    /// </summary>
    public long WorkshopId { get; set; } = -1;
    
    /// <summary>
    /// Gets a list of additional directory paths to include during compilation.
    /// </summary>
    public List<string> IncludePaths { get; } = new();

    /// <summary>
    /// Gets the list of directory paths searched for configuration files (extracted from settings.json).
    /// </summary>
    public List<string> IniRoots { get; } = new();

    /// <summary>
    /// Gets a list of mod names that should be cleaned (binaries removed) before building.
    /// </summary>
    public List<string> CleanMods { get; } = new();

    /// <summary>
    /// Gets or sets the list of dependent packages for the mod (used for Two-Pass compilation).
    /// </summary>
    public List<string> DependentPackages { get; set; } = new();

    /// <summary>
    /// Gets or sets the raw JSON content for asset-specific configuration (ContentOptions).
    /// </summary>
    public string? ContentOptionsJson { get; set; }

    /// <summary>
    /// Gets a list of custom actions to execute before the UnrealEd 'make' commandlet starts.
    /// </summary>
    public List<Action> PreMakeHooks { get; } = new();

    /// <summary>
    /// Gets or sets an optional override path for the build cache (fingerprints, trackers, etc.).
    /// </summary>
    public string? BuildCachePathOverride { get; set; }
    
    /// <summary>
    /// Gets the canonicalized mod name (whitespace and semicolons removed).
    /// </summary>
    public string ModNameCanonical => ModName.Replace(" ", "").Replace(";", "");

    /// <summary>
    /// Gets the absolute path to the mod's source root within the project.
    /// </summary>
    public string ModSrcRoot => Path.Combine(ProjectRoot, ModNameCanonical);

    /// <summary>
    /// Gets the absolute path to the mod's staging directory within the SDK.
    /// </summary>
    public string StagingPath => Path.Combine(SdkPath, "XComGame", "Mods", ModNameCanonical);

    /// <summary>
    /// Gets the absolute path where the final mod binaries and artifacts are placed.
    /// </summary>
    public string FinalModPath => Path.Combine(ModDestinationPath, ModNameCanonical);

    /// <summary>
    /// Gets the absolute path to the SDK's internal cooker output directory.
    /// </summary>
    public string CookerOutputPath => Path.Combine(SdkPath, "XComGame", "Published", "CookedPCConsole");

    /// <summary>
    /// Gets the absolute path to the active build cache directory.
    /// </summary>
    public string BuildCachePath => BuildCachePathOverride ?? Path.Combine(ProjectRoot, "BuildCache");

    /// <summary>
    /// Gets the absolute path to the UnrealEd 'XComGame.com' commandlet executable.
    /// </summary>
    public string CommandletPath => Path.Combine(SdkPath, "binaries", "Win64", "XComGame.com");

    /// <summary>
    /// Synchronizes the common paths to the underlying <see cref="ParserSettings"/>.
    /// </summary>
    public void SyncToParserSettings()
    {
        ParserSettings.LocalSrcRoot = Path.Combine(ModSrcRoot, "Src");
        ParserSettings.SdkRoot = SdkPath;
        ParserSettings.ModsCompiledAgainst = new List<string>(IncludePaths);
        // We'll let SettingsLoader handle AllModsRoot and Highlander paths from workspace settings if available,
        // but we can override them here if needed.
    }

    /// <summary>
    /// Validates the build options, ensuring all required paths exist.
    /// </summary>
    /// <returns>A list of validation errors, or an empty list if valid.</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ModName)) errors.Add("ModName is required.");
        if (string.IsNullOrWhiteSpace(ProjectRoot)) errors.Add("ProjectRoot is required.");
        if (!Directory.Exists(ProjectRoot)) errors.Add($"ProjectRoot does not exist: {ProjectRoot}");
        
        if (string.IsNullOrWhiteSpace(SdkPath)) errors.Add("SdkPath is required.");
        else if (!Directory.Exists(SdkPath)) errors.Add($"SdkPath does not exist: {SdkPath}");

        if (string.IsNullOrWhiteSpace(GamePath)) errors.Add("GamePath is required.");
        else if (!Directory.Exists(GamePath)) errors.Add($"GamePath does not exist: {GamePath}");

        if (string.IsNullOrWhiteSpace(ModDestinationPath)) errors.Add("ModDestinationPath is required.");

        return errors;
    }
}
