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

[X2ModCompiler.DependantPackages]
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
        var content = "[UnrealEd.EditorEngine]\n+ModEditPackages=A\n\n[X2ModCompiler.DependantPackages]\n\n[Other]";
        var handler = new IniHandler(".", new[] { "." });
        Assert.False(handler.IsTwoPassNeeded(content));
    }

    [Fact]
    public void PreparePass1Ini_StripsDependentSection_AndDoesNotAddMainMod()
    {
        var handler = new IniHandler(".", new[] { "." });
        var dependents = new List<string> { "D", "E" };
        var result = handler.PreparePass1Ini(OriginalContent, "MainModPackage", dependents);
        
        Assert.DoesNotContain("[X2ModCompiler.DependantPackages]", result);
        Assert.DoesNotContain("+ModEditPackages=D", result);
        Assert.Contains("+ModEditPackages=MainModPackage", result);
        Assert.Contains("[OtherSection]", result);
    }

    [Fact]
    public void PreparePass2Ini_AppendsMissingPackages_ButKeepsExisting()
    {
        var handler = new IniHandler(".", new[] { "." });
        var dependents = new List<string> { "D", "E" };
        var result = handler.PreparePass2Ini(OriginalContent, "MainModPackage", dependents);
        
        Assert.DoesNotContain("[X2ModCompiler.DependantPackages]", result);
        
        // MainModPackage already existed in OriginalContent, so it should be in its original position (before D, E)
        // D and E were in the DependantPackages section, so they should be added to the end of EditorEngine
        var lines = result.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        int mainIndex = -1;
        int dIndex = -1;
        int eIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("MainModPackage")) mainIndex = i;
            if (lines[i].Contains("=D")) dIndex = i;
            if (lines[i].Contains("=E")) eIndex = i;
        }
        
        Assert.True(mainIndex != -1, "MainModPackage should be present");
        Assert.True(mainIndex < dIndex, "Existing MainModPackage should remain in its original position (before added dependents)");
        Assert.True(dIndex < eIndex, "Dependent D should be before E");
    }

    [Fact]
    public void PreparePass2Ini_AddsMainMod_IfMissing()
    {
        var contentWithoutMain = "[UnrealEd.EditorEngine]\n+ModEditPackages=A\n";
        var handler = new IniHandler(".", new[] { "." });
        var dependents = new List<string> { "D" };
        var result = handler.PreparePass2Ini(contentWithoutMain, "MissingMod", dependents);
        
        Assert.Contains("+ModEditPackages=MissingMod", result);
        Assert.Contains("+ModEditPackages=D", result);
    }

    [Fact]
    public void PreparePass2Ini_AddsMainMod_EvenIfCommented()
    {
        var content = @"[UnrealEd.EditorEngine]
+ModEditPackages=FrostDivision
; +ModEditPackages=AdventCoalition
+ModEditPackages=BioDivision";
        
        var handler = new IniHandler(".", new[] { "." });
        var result = handler.PreparePass2Ini(content, "AdventCoalition", new List<string>());
        
        // Should contain the active one
        Assert.Contains("+ModEditPackages=AdventCoalition", result);
        // And keep the comment (standard behavior)
        Assert.Contains("; +ModEditPackages=AdventCoalition", result);
    }

    [Fact]
    public void FindTargetIni_PicksBestFile_WhenMultipleFilesFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var dir1 = Path.Combine(tempDir, "0Base");
            var dir2 = Path.Combine(tempDir, "1AdventCoalition");
            Directory.CreateDirectory(dir1);
            Directory.CreateDirectory(dir2);
            
            // dir1 has a generic one
            File.WriteAllText(Path.Combine(dir1, "XComEngine.ini"), "[UnrealEd.EditorEngine]");
            // dir2 has one with DependantPackages (higher priority)
            File.WriteAllText(Path.Combine(dir2, "XComEngine.ini"), "[X2ModCompiler.DependantPackages]");
            
            var handler = new IniHandler(tempDir, new[] { dir1, dir2 });
            var result = handler.FindTargetIni();
            
            Assert.Equal(Path.Combine(dir2, "XComEngine.ini"), result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
