using System.Text.Json;
using System.Text.RegularExpressions;

namespace XCom2ConfigParser2.Configuration;

/// <summary>
/// Defines the global configuration settings for the XCom2ConfigParser2 tool, typically loaded from <c>.vscode/settings.json</c>.
/// </summary>
public sealed class ParserSettings
{
    /// <summary>Gets or sets the list of directories to scan for .ini files. Defaults to <c>["Config"]</c>.</summary>
    public List<string> IniRoots { get; set; } = new() { "Config" };

    /// <summary>Gets or sets the relative path to the local source directory. Defaults to <c>"Src"</c>.</summary>
    public string LocalSrcRoot { get; set; } = "Src";

    /// <summary>Gets or sets the path to the PowerShell build script used for dependency discovery. Defaults to <c>".scripts/build.ps1"</c>.</summary>
    public string BuildScriptPath { get; set; } = ".scripts/build.ps1";

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

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsLoader"/> class.
    /// </summary>
    /// <param name="projectRoot">The absolute path to the project root directory.</param>
    public SettingsLoader(string projectRoot)
    {
        _projectRoot = projectRoot;
    }

    /// <summary>
    /// Loads the configuration from the workspace settings file, applying default values and resolving relative paths.
    /// </summary>
    /// <returns>A fully populated <see cref="ParserSettings"/> object.</returns>
    public ParserSettings Load()
    {
        var settings = new ParserSettings();
        var settingsFile = Path.Combine(_projectRoot, ".vscode", "settings.json");

        if (!File.Exists(settingsFile))
            return settings;

        try
        {
            string content = File.ReadAllText(settingsFile);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Load from JSON using case-insensitive property matching
            
            // Load iniRoots
            if (TryGetPropertyCaseInsensitive(root, "xcom.configParser.iniRoots", out var iniRoots))
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
            if (TryGetPropertyCaseInsensitive(root, "xcom.configParser.localSrcRoot", out var localSrcRoot))
            {
                string? path = localSrcRoot.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.LocalSrcRoot = ResolvePath(path);
            }

            // Load buildScriptPath
            if (TryGetPropertyCaseInsensitive(root, "xcom.configParser.buildScriptPath", out var buildScriptPath))
            {
                string? path = buildScriptPath.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.BuildScriptPath = ResolvePath(path);
            }

            // Load modsCompiledAgainst (explicit list takes precedence over build.ps1)
            if (TryGetPropertyCaseInsensitive(root, "xcom.configParser.modsCompiledAgainst", out var modsCompiledAgainst))
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
                // Parse from build.ps1
                settings.ModsCompiledAgainst = ParseBuildScript(settings.BuildScriptPath);
            }

            // Load communityHighlanderPath
            if (TryGetPropertyCaseInsensitive(root, "xcom.configParser.communityHighlanderPath", out var communityHighlanderPath))
            {
                string? path = communityHighlanderPath.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.CommunityHighlanderPath = ResolvePath(path);
            }

            // Load alienHighlanderPath
            if (TryGetPropertyCaseInsensitive(root, "xcom.configParser.alienHighlanderPath", out var alienHighlanderPath))
            {
                string? path = alienHighlanderPath.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.AlienHighlanderPath = ResolvePath(path);
            }

            // Load allModsRoot
            if (TryGetPropertyCaseInsensitive(root, "xcom.configParser.allModsRoot", out var allModsRoot))
            {
                string? path = allModsRoot.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.AllModsRoot = ResolvePath(path);
            }

            // Load cachePath
            if (TryGetPropertyCaseInsensitive(root, "xcom.configParser.cachePath", out var cachePath))
            {
                string? path = cachePath.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.CachePath = ResolvePath(path);
            }

            // Load sdkRoot (legacy key)
            if (TryGetPropertyCaseInsensitive(root, "xcom.highlander.sdkroot", out var sdkRoot))
            {
                string? path = sdkRoot.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.SdkRoot = ResolvePath(path);
            }
        }
        catch (JsonException ex)
        {
            // Return defaults on parse error but flag it
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

        // Expand ~ to home directory
        if (path.StartsWith("~"))
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = Path.Combine(homeDir, path.Substring(1).TrimStart('/', '\\'));
        }

        // Replace ${workspaceFolder} with project root
        if (path.Contains("${workspaceFolder}"))
        {
            path = path.Replace("${workspaceFolder}", _projectRoot);
        }

        // Resolve relative paths (only if not already rooted)
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(_projectRoot, path);
        }

        // Normalize path separators
        path = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        return path;
    }

    private List<string> ParseBuildScript(string buildScriptPath)
    {
        var paths = new List<string>();
        
        if (!File.Exists(buildScriptPath))
            return paths;

        try
        {
            string content = File.ReadAllText(buildScriptPath);
            // Parse line by line to properly handle comments
            foreach (string line in content.Split('\n'))
            {
                string trimmed = line.Trim();
                // Skip commented lines
                if (trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                    continue;
                    
                // Match $builder.IncludeSrc("path") - escape $ for regex
                var match = Regex.Match(trimmed, @"\$builder\.IncludeSrc\s*\(\s*[""']([^""']+)[""']\s*\)");
                if (match.Success)
                {
                    string path = match.Groups[1].Value;
                    paths.Add(ResolvePath(path));
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return paths;
    }
}
