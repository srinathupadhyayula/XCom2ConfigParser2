using System.Text.Json;
using System.Text.Json.Serialization;

namespace XCom2ModCompiler.Utilities;

/// <summary>
/// Provides asynchronous utility methods for serializing and deserializing JSON data.
/// Optimized for build environment persistence using indented formatting and case-insensitivity.
/// </summary>
public static class JsonUtilities
{
    /// <summary>
    /// Configuration options for JSON serialization and deserialization.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>PropertyNameCaseInsensitive: Enables case-insensitive matching of property names during deserialization.</description></item>
    /// <item><description>WriteIndented: Formats the JSON output with indentation for readability.</description></item>
    /// <item><description>DefaultIgnoreCondition: Ignores properties with null values during serialization.</description></item>
    /// </list>
    /// </remarks>
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Asynchronously deserializes a JSON file into the specified type.
    /// </summary>
    /// <typeparam name="T">The target type for deserialization.</typeparam>
    /// <param name="filePath">The absolute path to the JSON file.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The deserialized object, or the default value of <typeparamref name="T"/> if loading fails.</returns>
    public static async Task<T?> LoadAsync<T>(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return default;

        try
        {
            using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, ct);
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return default (will trigger a rebuild)
            return default;
        }
    }

    /// <summary>
    /// Asynchronously serializes an object to a JSON file, creating parent directories if needed.
    /// </summary>
    /// <typeparam name="T">The type of data to serialize.</typeparam>
    /// <param name="filePath">The absolute path where the JSON should be saved.</param>
    /// <param name="data">The object to serialize.</param>
    /// <param name="ct">The cancellation token.</param>
    public static async Task SaveAsync<T>(string filePath, T data, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, data, Options, ct);
    }
}
