using System.Text.Json;
using System.Text.RegularExpressions;

namespace XCom2ModCompiler.Configuration;

/// <summary>
/// Configuration settings loaded from .vscode/settings.json.
/// Reused and adapted from XCom2ConfigParser2.
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
    public string CachePath { get; set; } = ".xcom2cache";
    public string? SdkRoot { get; set; }
    public bool HasJsonParseError { get; set; }
    public string? JsonParseErrorMessage { get; set; }
}

/// <summary>
/// Loads configuration from .vscode/settings.json.
/// Reused and adapted from XCom2ConfigParser2.
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

            // Load modsCompiledAgainst
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

            // Load sdkRoot
            if (TryGetPropertyCaseInsensitive(root, "xcom.highlander.sdkroot", out var sdkRoot))
            {
                string? path = sdkRoot.GetString();
                if (!string.IsNullOrEmpty(path))
                    settings.SdkRoot = ResolvePath(path);
            }
        }
        catch (JsonException ex)
        {
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

        path = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        return path;
    }
}
