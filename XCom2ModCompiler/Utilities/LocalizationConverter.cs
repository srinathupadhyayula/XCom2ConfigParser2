using System.IO;
using System.Linq;
using System.Text;

namespace XCom2ModCompiler.Utilities;

public static class LocalizationConverter
{
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
