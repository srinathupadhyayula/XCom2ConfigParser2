using System.Text.Json;
using System.Text.RegularExpressions;

namespace XCom2ConfigParser2.Configuration;

/// <summary>
/// Configuration settings loaded from .vscode/settings.json.
/// </summary>
public sealed class ParserSettings
{
    public List<string> IniRoots { get; set; } = new() { "Config" };
    public string LocalSrcRoot { get; set; } = "Src";
    public string BuildScriptPath { get; set; } = ".scripts/build.ps1";
    public List<string> ModsCompiledAgainst { get; set; } = new();
    public string? CommunityHighlanderPath { get; set; }
    public string? AlienHighlanderPath { get; set; }
    public string AllModsRoot { get; set; } = "../../Mods/";
    public string CachePath { get; set; } = ".xcom2cache/structs/";
    public string? SdkRoot { get; set; }
    public bool HasJsonParseError { get; set; }
    public string? JsonParseErrorMessage { get; set; }
}

/// <summary>
/// Loads configuration from .vscode/settings.json.
/// </summary>
public sealed class SettingsLoader
{
    private readonly string _projectRoot;

    public SettingsLoader(string projectRoot)
    {
        _projectRoot = projectRoot;
    }

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

            // Load iniRoots
            if (root.TryGetProperty("xcom.configParser.iniRoots", out var iniRoots))
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
            if (root.TryGetProperty("xcom.configParser.localSrcRoot", out var localSrcRoot))
            {
                string? path = localSrcRoot.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.LocalSrcRoot = ResolvePath(path);
            }

            // Load buildScriptPath
            if (root.TryGetProperty("xcom.configParser.buildScriptPath", out var buildScriptPath))
            {
                string? path = buildScriptPath.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.BuildScriptPath = ResolvePath(path);
            }

            // Load modsCompiledAgainst (explicit list takes precedence over build.ps1)
            if (root.TryGetProperty("xcom.configParser.modsCompiledAgainst", out var modsCompiledAgainst))
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
            if (root.TryGetProperty("xcom.configParser.communityHighlanderPath", out var communityHighlanderPath))
            {
                string? path = communityHighlanderPath.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.CommunityHighlanderPath = ResolvePath(path);
            }

            // Load alienHighlanderPath
            if (root.TryGetProperty("xcom.configParser.alienHighlanderPath", out var alienHighlanderPath))
            {
                string? path = alienHighlanderPath.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.AlienHighlanderPath = ResolvePath(path);
            }

            // Load allModsRoot
            if (root.TryGetProperty("xcom.configParser.allModsRoot", out var allModsRoot))
            {
                string? path = allModsRoot.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.AllModsRoot = ResolvePath(path);
            }

            // Load cachePath
            if (root.TryGetProperty("xcom.configParser.cachePath", out var cachePath))
            {
                string? path = cachePath.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.CachePath = ResolvePath(path);
            }

            // Load sdkRoot (legacy key)
            if (root.TryGetProperty("xcom.highlander.sdkroot", out var sdkRoot))
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
