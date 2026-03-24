namespace XCom2ModCompiler.Configuration;

/// <summary>
/// Encapsulates all global build configuration options and computed paths for the XCom2ModCompiler.
/// This class serves as the central configuration object passed across the build pipeline components.
/// </summary>
public class BuildOptions
{
    /// <summary>
    /// Gets or sets the display name of the mod.
    /// </summary>
    public string ModName { get; init; } = "";

    /// <summary>
    /// Gets or sets the absolute path to the root directory containing mod project source files.
    /// </summary>
    public string ProjectRoot { get; init; } = "";

    /// <summary>
    /// Gets or sets the absolute path to the XCOM 2 SDK installation.
    /// </summary>
    public string SdkPath { get; init; } = "";

    /// <summary>
    /// Gets or sets the absolute path to the XCOM 2 game installation.
    /// </summary>
    public string GamePath { get; init; } = "";

    /// <summary>
    /// Gets or sets the absolute path where the final mod artifacts should be deployed.
    /// </summary>
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
    /// Gets or sets the Steam Workshop ID for the mod. Set to -1 for local-only mods.
    /// </summary>
    public long WorkshopId { get; set; } = -1;
    
    /// <summary>
    /// Gets a list of additional directory paths to include during compilation.
    /// </summary>
    public List<string> IncludePaths { get; } = new();

    /// <summary>
    /// Gets a list of mod names that should be cleaned (binaries removed) before building.
    /// </summary>
    public List<string> CleanMods { get; } = new();

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
}
