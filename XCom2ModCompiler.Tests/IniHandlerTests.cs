using Xunit;
using XCom2ModCompiler.Utilities;
using XCom2ModCompiler.Exceptions;
using System.IO;
using System.Collections.Generic;

namespace XCom2ModCompiler.Tests;

public class IniHandlerTests
{
    private const string OriginalContent = @"[UnrealEd.EditorEngine]
+ModEditPackages=A
+ModEditPackages=B
+ModEditPackages=MainModPackage

[X2Compiler.DependantPackages]
+ModEditPackages=D
+ModEditPackages=E

[OtherSection]
Key=Value";

    [Fact]
    public void IsTwoPassNeeded_ReturnsTrue_WhenSectionHasPackages()
    {
        var handler = new IniHandler(".", new[] { "." });
        Assert.True(handler.IsTwoPassNeeded(OriginalContent));
    }

    [Fact]
    public void IsTwoPassNeeded_ReturnsFalse_WhenSectionIsEmpty()
    {
        var content = "[UnrealEd.EditorEngine]\n+ModEditPackages=A\n\n[X2Compiler.DependantPackages]\n\n[Other]";
        var handler = new IniHandler(".", new[] { "." });
        Assert.False(handler.IsTwoPassNeeded(content));
    }

    [Fact]
    public void PreparePass1Content_StripsDependentSection()
    {
        var handler = new IniHandler(".", new[] { "." });
        var result = handler.PreparePass1Content(OriginalContent);
        
        Assert.DoesNotContain("[X2Compiler.DependantPackages]", result);
        Assert.DoesNotContain("+ModEditPackages=D", result);
        Assert.Contains("+ModEditPackages=MainModPackage", result);
        Assert.Contains("[OtherSection]", result);
    }

    [Fact]
    public void PreparePass2Content_AppendsMainModAndDependentsToEditorEngine()
    {
        var handler = new IniHandler(".", new[] { "." });
        var result = handler.PreparePass2Content(OriginalContent, "NewMainMod");
        
        Assert.DoesNotContain("[X2Compiler.DependantPackages]", result);
        
        // Should be appended after existing packages, starting with NewMainMod
        var lines = result.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        int mainIndex = -1;
        int dIndex = -1;
        int eIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("NewMainMod")) mainIndex = i;
            if (lines[i].Contains("=D")) dIndex = i;
            if (lines[i].Contains("=E")) eIndex = i;
        }
        
        Assert.True(mainIndex != -1, "NewMainMod should be present in Pass 2");
        Assert.True(mainIndex < dIndex, "NewMainMod should be BEFORE dependent D");
        Assert.True(dIndex < eIndex, "Dependent D should be BEFORE dependent E");
        Assert.Contains("[OtherSection]", result);
    }

    [Fact]
    public void FindTargetIni_ThrowsBuildConfigurationException_WhenMultipleFilesFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var dir1 = Path.Combine(tempDir, "Dir1");
            var dir2 = Path.Combine(tempDir, "Dir2");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);
            
            File.WriteAllText(Path.Combine(dir1, "XComEngine.ini"), "[UnrealEd.EditorEngine]");
            File.WriteAllText(Path.Combine(dir2, "XComEngine.ini"), "[X2Compiler.DependantPackages]");
            
            var handler = new IniHandler(tempDir, new[] { dir1, dir2 });
            Assert.Throws<BuildConfigurationException>(() => handler.FindTargetIni());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
