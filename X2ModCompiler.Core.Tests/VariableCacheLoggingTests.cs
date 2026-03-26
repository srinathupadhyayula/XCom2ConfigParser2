using Xunit;
using X2ModCompiler.Core.StructValidation;
using System.IO;

namespace X2ModCompiler.Core.Tests;

public class VariableCacheLoggingTests
{
    [Fact]
    public void Clear_DoesNotThrow_WhenFileDeleteFails()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var cache = new VariableCache(tempDir);
        
        // Create a file that will be locked
        var testFile = Path.Combine(tempDir, "test.mempack");
        File.WriteAllBytes(testFile, new byte[] { 1, 2, 3 });
        
        // Lock the file
        using var fileStream = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None);
        
        // Act & Assert - should not throw
        var exception = Record.Exception(() => cache.Clear());
        
        // Cleanup
        fileStream.Dispose();
        try { Directory.Delete(tempDir, true); } catch { }
        
        Assert.Null(exception);
    }

    [Fact]
    public void ClearNegativeEntries_DoesNotThrow_WhenFileReadFails()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var cache = new VariableCache(tempDir);
        
        // Create a file that will be locked
        var testFile = Path.Combine(tempDir, "test.mempack");
        File.WriteAllBytes(testFile, new byte[] { 1, 2, 3 });
        
        // Lock the file
        using var fileStream = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None);
        
        // Act & Assert - should not throw
        var exception = Record.Exception(() => cache.ClearNegativeEntries());
        
        // Cleanup
        fileStream.Dispose();
        try { Directory.Delete(tempDir, true); } catch { }
        
        Assert.Null(exception);
    }
}
