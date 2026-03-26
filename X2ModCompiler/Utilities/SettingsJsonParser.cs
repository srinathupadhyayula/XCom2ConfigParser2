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
}
