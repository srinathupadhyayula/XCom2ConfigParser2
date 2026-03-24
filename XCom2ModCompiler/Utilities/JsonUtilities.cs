using System.Text.Json;
using System.Text.Json.Serialization;

namespace XCom2ModCompiler.Utilities;

public static class JsonUtilities
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
