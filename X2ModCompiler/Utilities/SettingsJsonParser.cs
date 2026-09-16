using System.Text.Json;

namespace X2ModCompiler.Utilities;

/// <summary>
/// Provides utilities for parsing VSCode settings.json configuration files.
/// </summary>
public static class SettingsJsonParser
{
    /// <summary>
    /// Extracts the list of INI root paths from a settings.json JSON string.
    /// </summary>
    /// <param name="json">The JSON content from settings.json.</param>
    /// <returns>A list of INI root paths, or an empty list if parsing fails or no roots are found.</returns>
    public static List<string> ExtractIniRoots(string json)
    {
        var roots = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions 
            { 
                AllowTrailingCommas = true, 
                CommentHandling = JsonCommentHandling.Skip 
            });
            
            return ExtractIniRoots(doc);
        }
        catch (JsonException)
        {
            // Return empty list for invalid JSON
            return roots;
        }
    }

    /// <summary>
    /// Extracts the list of INI root paths from a parsed JsonDocument.
    /// </summary>
    /// <param name="doc">The parsed JsonDocument.</param>
    /// <returns>A list of INI root paths, or an empty list if no roots are found.</returns>
    public static List<string> ExtractIniRoots(JsonDocument doc)
    {
        var roots = new List<string>();
        
        if (doc.RootElement.TryGetProperty("X2ModCompiler.iniRoots", out var iniRootsProperty) 
            && iniRootsProperty.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in iniRootsProperty.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var value = element.GetString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        roots.Add(value);
                    }
                }
            }
        }

        return roots;
    }

    /// <summary>
    /// Extracts the log verbosity from settings.json.
    /// </summary>
    public static X2ModCompiler.Core.Configuration.CompilerLogLevel ExtractLogVerbosity(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions 
            { 
                AllowTrailingCommas = true, 
                CommentHandling = JsonCommentHandling.Skip 
            });
            
            if (doc.RootElement.TryGetProperty("X2ModCompiler.logVerbosity", out var prop) 
                && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString() ?? "Information";
                if (Enum.TryParse<X2ModCompiler.Core.Configuration.CompilerLogLevel>(value, true, out var level))
                    return level;
            }
        }
        catch (JsonException)
        {
            // Ignore
        }
        return X2ModCompiler.Core.Configuration.CompilerLogLevel.Information;
    }

    /// <summary>
    /// Extracts the AllModsRoot path from settings.json.
    /// </summary>
    public static string ExtractAllModsRoot(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions 
            { 
                AllowTrailingCommas = true, 
                CommentHandling = JsonCommentHandling.Skip 
            });
            
            if (doc.RootElement.TryGetProperty("X2ModCompiler.allModsRoot", out var prop) 
                && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
            // Ignore
        }
        return "";
    }

    /// <summary>
    /// Extracts the CommunityHighlander path from settings.json.
    /// </summary>
    public static string ExtractCommunityHighlanderPath(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions 
            { 
                AllowTrailingCommas = true, 
                CommentHandling = JsonCommentHandling.Skip 
            });
            
            if (doc.RootElement.TryGetProperty("X2ModCompiler.communityHighlanderPath", out var prop) 
                && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
            // Ignore
        }
        return "";
    }

    /// <summary>
    /// Extracts the AlienHighlander path from settings.json.
    /// </summary>
    public static string ExtractAlienHighlanderPath(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions 
            { 
                AllowTrailingCommas = true, 
                CommentHandling = JsonCommentHandling.Skip 
            });
            
            if (doc.RootElement.TryGetProperty("X2ModCompiler.alienHighlanderPath", out var prop) 
                && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
            // Ignore
        }
        return "";
    }
}
