using System.IO;
using System.Text;
using System.Threading.Tasks;
using X2ModCompiler.Utilities;
using Xunit;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Tests;

public class LocalizationConverterTests : TestBase
{
    [Fact]
    public async Task Convert_ConvertsUtf8ToUtf16Le()
    {
        // Arrange
        await using var temp = new TempDirectory();
        var locDir = Path.Combine(temp.Path, "Localization");
        Directory.CreateDirectory(locDir);
        
        var testFile = Path.Combine(locDir, "Test.int");
        string originalContent = "Test Content with specialized characters: éàü";
        File.WriteAllText(testFile, originalContent, Encoding.UTF8);

        // Act
        LocalizationConverter.Convert(temp.Path);

        // Assert
        byte[] bytes = File.ReadAllBytes(testFile);
        
        // UTF-16 LE BOM is 0xFF 0xFE
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xFE, bytes[1]);

        string convertedContent = File.ReadAllText(testFile, Encoding.Unicode);
        Assert.Equal(originalContent, convertedContent);
    }
}
