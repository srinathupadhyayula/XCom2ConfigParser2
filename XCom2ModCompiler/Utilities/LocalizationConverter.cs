using System.IO;
using System.Linq;
using System.Text;

namespace XCom2ModCompiler.Utilities;

/// <summary>
/// Provides utilities for converting localization files into the encoding format required by the XCOM 2 engine.
/// Unreal Engine 3 (XCOM 2 version) requires localization files to be encoded as UTF-16 LE with BOM.
/// </summary>
public static class LocalizationConverter
{
    /// <summary>
    /// Converts all localization files in the specified staging path to UTF-16 LE with BOM.
    /// Files that are already UTF-16 encoded are skipped to prevent data corruption.
    /// </summary>
    /// <param name="stagingPath">The absolute path to the mod's staging directory.</param>
    public static void Convert(string stagingPath)
    {
        var locPath = Path.Combine(stagingPath, "Localization");
        if (!Directory.Exists(locPath)) return;

        var files = Directory.GetFiles(locPath, "*.*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (IsUtf16(file))
            {
                // Already UTF-16, skip to avoid corruption
                continue;
            }

            var content = File.ReadAllText(file, Encoding.UTF8);
            // Write as UTF-16 LE with BOM (Unicode in .NET)
            File.WriteAllText(file, content, Encoding.Unicode);
        }
    }

    /// <summary>
    /// Detects if a file is encoded in UTF-16 by checking for the Byte Order Mark (BOM).
    /// Supports both Little-Endian (LE) and Big-Endian (BE) BOMs.
    /// </summary>
    /// <param name="filePath">The absolute path to the file to check.</param>
    /// <returns>True if a UTF-16 BOM is detected; otherwise false.</returns>
    private static bool IsUtf16(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            if (fs.Length < 2) return false;

            var bom = new byte[2];
            fs.ReadExactly(bom);
            
            // UTF-16 LE BOM (0xFF 0xFE) or UTF-16 BE BOM (0xFE 0xFF)
            return (bom[0] == 0xFF && bom[1] == 0xFE) || (bom[0] == 0xFE && bom[1] == 0xFF);
        }
        catch
        {
            return false;
        }
    }
}
