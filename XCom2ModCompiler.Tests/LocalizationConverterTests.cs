using System.IO;
using System.Text;
using XCom2ModCompiler.Utilities;
using Xunit;

namespace XCom2ModCompiler.Tests;

public class LocalizationConverterTests
{
    [Fact]
    public void Convert_ConvertsUtf8ToUtf16Le()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var locDir = Path.Combine(tempDir, "Localization");
        Directory.CreateDirectory(locDir);
        
        var testFile = Path.Combine(locDir, "Test.int");
        string originalContent = "Test Content with specialized characters: éàü";
        File.WriteAllText(testFile, originalContent, Encoding.UTF8);

        // Act
        LocalizationConverter.Convert(tempDir);

        // Assert
        byte[] bytes = File.ReadAllBytes(testFile);
        
        // UTF-16 LE BOM is 0xFF 0xFE
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xFE, bytes[1]);

        string convertedContent = File.ReadAllText(testFile, Encoding.Unicode);
        Assert.Equal(originalContent, convertedContent);

        // Cleanup
        Directory.Delete(tempDir, true);
    }
}
