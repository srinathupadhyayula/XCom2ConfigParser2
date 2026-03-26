using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace X2ModCompiler.Core.Configuration;

/// <summary>
/// Defines the global configuration settings for the X2ModCompiler.Core tool, typically loaded from <c>.vscode/settings.json</c>.
/// </summary>
public sealed class ParserSettings
{
    /// <summary>Gets or sets the list of directories to scan for .ini files. Defaults to <c>["Config"]</c>.</summary>
    public List<string> IniRoots { get; set; } = new() { "Config" };

    /// <summary>Gets or sets the relative path to the local source directory. Defaults to <c>"Src"</c>.</summary>
    public string LocalSrcRoot { get; set; } = "Src";

    /// <summary>Gets or sets the path to the PowerShell build script used for dependency discovery. Defaults to <c>".scripts/build.ps1"</c>.</summary>
    public string BuildScriptPath { get; set; } = Path.Combine(".scripts", "build.ps1");

    /// <summary>Gets or sets the explicit list of external mods this project is compiled against.</summary>
    public List<string> ModsCompiledAgainst { get; set; } = new();

    /// <summary>Gets or sets the absolute path to the Community Highlander source, if applicable.</summary>
    public string? CommunityHighlanderPath { get; set; }

    /// <summary>Gets or sets the absolute path to the Alien Highlander source, if applicable.</summary>
    public string? AlienHighlanderPath { get; set; }

    /// <summary>Gets or sets the shared root directory for all external mods. Used for relative path resolution.</summary>
    public string AllModsRoot { get; set; } = "../../Mods/";

    /// <summary>Gets or sets the directory where persistent struct and variable caches are stored.</summary>
    public string CachePath { get; set; } = ".xcom2cache";

    /// <summary>Gets or sets the root path of the XCOM 2 WOTC SDK.</summary>
    public string? SdkRoot { get; set; }

    /// <summary>Gets or sets the log verbosity level for the compiler. Defaults to <see cref="CompilerLogLevel.Debug"/>.</summary>
    public CompilerLogLevel LogVerbosity { get; set; } = CompilerLogLevel.Debug;

    /// <summary>
    /// Gets a value indicating whether an error occurred during the last attempt to parse the configuration file.
    /// </summary>
    public bool HasJsonParseError { get; set; }

    /// <summary>Gets the descriptive error message if <see cref="HasJsonParseError"/> is <c>true</c>.</summary>
    public string? JsonParseErrorMessage { get; set; }
}

/// <summary>
/// Responsible for loading and resolving configuration from the workspace <c>.vscode/settings.json</c> file.
/// </summary>
/// <remarks>
/// This loader supports case-insensitive JSON property lookup and automatic resolution of VSCode-style 
/// variables like <c>${workspaceFolder}</c>. It also features a fallback mechanism to extract 
/// dependencies from a PowerShell build script if they aren't explicitly defined in the settings.
/// </remarks>
public sealed class SettingsLoader
{
    private readonly string _projectRoot;
    private readonly ILogger<SettingsLoader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsLoader"/> class.
    /// </summary>
    /// <param name="projectRoot">The absolute path to the project root directory.</param>
    /// <param name="logger">The logger instance.</param>
    public SettingsLoader(string projectRoot, ILogger<SettingsLoader> logger)
    {
        _projectRoot = projectRoot;
        _logger = logger;
    }

    /// <summary>
    /// Loads the configuration from the workspace settings file, applying default values and resolving relative paths.
    /// </summary>
    /// <returns>A fully populated <see cref="ParserSettings"/> object.</returns>
    public ParserSettings Load()
    {
        _logger.LogInformation("Loading settings from {ProjectRoot}", _projectRoot);
        var settings = new ParserSettings();
        var settingsFile = Path.Combine(_projectRoot, ".vscode", "settings.json");

        if (!File.Exists(settingsFile))
        {
            _logger.LogWarning("Settings file not found: {SettingsFile}. Using defaults.", settingsFile);
            return settings;
        }

        try
        {
            string content = File.ReadAllText(settingsFile);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Load iniRoots
            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.iniRoots", out var iniRoots))
            {
                settings.IniRoots = new List<string>();
                foreach (var element in iniRoots.EnumerateArray())
                {
                    string? path = element.GetString();
                    if (!string.IsNullOrEmpty(path))
                        settings.IniRoots.Add(ResolvePath(path));
                }
            }

            // Load localSrcRoot
            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.localSrcRoot", out var localSrcRoot))
            {
                string? path = localSrcRoot.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.LocalSrcRoot = ResolvePath(path);
            }

            // Load buildScriptPath
            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.buildScriptPath", out var buildScriptPath))
            {
                string? path = buildScriptPath.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.BuildScriptPath = ResolvePath(path);
            }

            // Load modsCompiledAgainst (explicit list takes precedence over build.ps1)
            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.modsCompiledAgainst", out var modsCompiledAgainst))
            {
                settings.ModsCompiledAgainst = new List<string>();
                foreach (var element in modsCompiledAgainst.EnumerateArray())
                {
                    string? path = element.GetString();
                    if (!string.IsNullOrEmpty(path))
                        settings.ModsCompiledAgainst.Add(ResolvePath(path));
                }
            }
            else
            {
                _logger.LogDebug("No explicit mods-compiled-against found. Parsing from build script: {BuildScriptPath}", settings.BuildScriptPath);
                settings.ModsCompiledAgainst = ParseBuildScript(settings.BuildScriptPath);
            }

            // Highlanders
            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.communityHighlanderPath", out var chPath))
                settings.CommunityHighlanderPath = ResolvePath(chPath.GetString() ?? "");

            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.alienHighlanderPath", out var ahPath))
                settings.AlienHighlanderPath = ResolvePath(ahPath.GetString() ?? "");

            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.allModsRoot", out var allModsRoot))
                settings.AllModsRoot = ResolvePath(allModsRoot.GetString() ?? "");

            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.cachePath", out var cachePath))
                settings.CachePath = ResolvePath(cachePath.GetString() ?? "");

            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.sdkRoot", out var sdkRoot))
                settings.SdkRoot = ResolvePath(sdkRoot.GetString() ?? "");

            // Load log verbosity
            if (TryGetPropertyCaseInsensitive(root, "X2ModCompiler.logVerbosity", out var logVerbosityElement))
            {
                string? logVerbosityStr = logVerbosityElement.GetString();
                if (!string.IsNullOrEmpty(logVerbosityStr) && Enum.TryParse<CompilerLogLevel>(logVerbosityStr, ignoreCase: true, out var logVerbosity))
                {
                    settings.LogVerbosity = logVerbosity;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse settings.json");
            settings.HasJsonParseError = true;
            settings.JsonParseErrorMessage = ex.Message;
        }

        return settings;
    }

    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (path.StartsWith("~"))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = Path.Combine(homeDir, path.Substring(1).TrimStart('/', '\\'));
        }

        if (path.Contains("${workspaceFolder}"))
        {
            path = path.Replace("${workspaceFolder}", _projectRoot);
        }

        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(_projectRoot, path);
        }

        // Normalize and validate path to prevent directory traversal attacks
        var fullPath = Path.GetFullPath(path);
        var normalizedProjectRoot = Path.GetFullPath(_projectRoot);

        // Ensure path doesn't escape project root (security check)
        if (!fullPath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path '{Path}' escapes project root '{ProjectRoot}'. This may be a security risk.", fullPath, normalizedProjectRoot);
        }

        return fullPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
    }

    private List<string> ParseBuildScript(string buildScriptPath)
    {
        var paths = new List<string>();
        if (!File.Exists(buildScriptPath)) return paths;

        try
        {
            string content = File.ReadAllText(buildScriptPath);
            foreach (string line in content.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("#") || trimmed.StartsWith("//")) continue;
                    
                var match = Regex.Match(trimmed, @"\$builder\.IncludeSrc\s*\(\s*[""']([^""']+)[""']\s*\)");
                if (match.Success)
                {
                    paths.Add(ResolvePath(match.Groups[1].Value));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse build script");
        }

        return paths;
    }
}
