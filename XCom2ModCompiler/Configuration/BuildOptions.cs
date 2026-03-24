namespace XCom2ModCompiler.Configuration;

/// <summary>
/// Build configuration options for XCom2ModCompiler.
/// </summary>
public class BuildOptions
{
    // Required
    public string ModName { get; init; } = "";
    public string ProjectRoot { get; init; } = "";
    public string SdkPath { get; init; } = "";
    public string GamePath { get; init; } = "";
    public string ModDestinationPath { get; init; } = "";
    
    // Configuration
    public bool Debug { get; set; }
    public bool FinalRelease { get; set; }
    public long WorkshopId { get; set; } = -1;
    
    // Dependencies
    public List<string> IncludePaths { get; } = new();
    public List<string> CleanMods { get; } = new();
    public string? ContentOptionsJson { get; set; }
    public List<Action> PreMakeHooks { get; } = new();
    public string? BuildCachePathOverride { get; set; }
    
    // Computed (set during initialization)
    public string ModNameCanonical => ModName.Replace(" ", "").Replace(";", "");
    public string ModSrcRoot => Path.Combine(ProjectRoot, ModNameCanonical);
    public string StagingPath => Path.Combine(SdkPath, "XComGame", "Mods", ModNameCanonical);
    public string FinalModPath => Path.Combine(ModDestinationPath, ModNameCanonical);
    public string CookerOutputPath => Path.Combine(SdkPath, "XComGame", "Published", "CookedPCConsole");
    public string BuildCachePath => BuildCachePathOverride ?? Path.Combine(ProjectRoot, "BuildCache");
    public string CommandletPath => Path.Combine(SdkPath, "binaries", "Win64", "XComGame.com");
}
