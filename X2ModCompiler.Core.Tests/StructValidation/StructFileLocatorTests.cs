using Shouldly;
using X2ModCompiler.Core.Configuration;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace X2ModCompiler.Core.Tests.Validation;

/// <summary>
/// Tests for <see cref="StructFileLocator"/> — searches configured source directories for struct definitions.
/// </summary>
public class StructFileLocatorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _srcDir;

    public StructFileLocatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"struct_locator_{Guid.NewGuid()}");
        _srcDir = Path.Combine(_tempDir, "Src");
        Directory.CreateDirectory(_srcDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void StructFileLocator_Locate_StructInLocalSrc_FindsFile()
    {
        CreateUcFile("TestPackage", "TestClass", "struct MyStruct { var int X; };");
        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var locator = new StructFileLocator(settings, NullLogger<StructFileLocator>.Instance);

        var result = locator.Locate("MyStruct");

        result.Found.ShouldBeTrue();
        result.Source.ShouldBe("LocalSrc");
        result.FilePath.ShouldEndWith("TestClass.uc");
    }

    [Fact]
    public void StructFileLocator_Locate_NonExistentStruct_ReturnsNotFound()
    {
        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var locator = new StructFileLocator(settings, NullLogger<StructFileLocator>.Instance);

        var result = locator.Locate("NonExistentStruct");

        result.Found.ShouldBeFalse();
    }

    [Fact]
    public void StructFileLocator_Locate_NativeStruct_FindsFile()
    {
        CreateUcFile("TestPackage", "TestClass", "struct native MyNativeStruct { var int X; };");
        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var locator = new StructFileLocator(settings, NullLogger<StructFileLocator>.Instance);

        var result = locator.Locate("MyNativeStruct");

        result.Found.ShouldBeTrue();
    }

    [Fact]
    public void StructFileLocator_Locate_WithKnownPackage_UsesPackageAwareSearch()
    {
        CreateUcFile("CorrectPackage", "MyClass", "struct TargetStruct { var bool Y; };");
        CreateUcFile("WrongPackage", "OtherClass", "struct TargetStruct { var int X; };");

        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var locator = new StructFileLocator(settings, NullLogger<StructFileLocator>.Instance);

        var result = locator.Locate("TargetStruct", "CorrectPackage");

        result.Found.ShouldBeTrue();
        result.FilePath!.ShouldContain("CorrectPackage");
    }

    [Fact]
    public void StructFileLocator_Locate_InModsCompiledAgainst_FindsStruct()
    {
        var modSrc = Path.Combine(_tempDir, "ModSrc");
        CreateUcFile("ModPackage", "ModClass", "struct ModStruct { var float Z; };", modSrc);

        var settings = new ParserSettings
        {
            ModsCompiledAgainst = new List<string> { modSrc }
        };
        var locator = new StructFileLocator(settings, NullLogger<StructFileLocator>.Instance);

        var result = locator.Locate("ModStruct");

        result.Found.ShouldBeTrue();
        result.Source.ShouldBe("ModCompiledAgainst");
    }

    [Fact]
    public void StructFileLocator_Locate_InaccessibleDirectory_DoesNotThrow()
    {
        // Point LocalSrcRoot at a path that doesn't exist — should gracefully return NotFound
        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_tempDir, "doesnotexist") };
        var locator = new StructFileLocator(settings, NullLogger<StructFileLocator>.Instance);

        // Act + Assert: no throw
        var result = locator.Locate("SomeStruct");
        result.Found.ShouldBeFalse();
    }

    [Fact]
    public void StructFileLocator_Locate_LocalSrcTakesPriorityOverMod()
    {
        var modSrc = Path.Combine(_tempDir, "ModSrc");
        // Same struct in both local and mod
        CreateUcFile("Pkg", "LocalClass", "struct SharedStruct { var int Local; };");
        CreateUcFile("Pkg", "ModClass", "struct SharedStruct { var int Mod; };", modSrc);

        var settings = new ParserSettings
        {
            LocalSrcRoot = _srcDir,
            ModsCompiledAgainst = new List<string> { modSrc }
        };
        var modSrcCache = new ModSrcPathCache(settings, NullLogger<ModSrcPathCache>.Instance);
        var locator = new StructFileLocator(settings, NullLogger<StructFileLocator>.Instance, modSrcCache);

        var result = locator.Locate("SharedStruct");

        result.Found.ShouldBeTrue();
        result.Source.ShouldBe("LocalSrc");
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private void CreateUcFile(string packageName, string className, string content, string? srcRoot = null)
    {
        var dir = Path.Combine(srcRoot ?? _srcDir, packageName, "Classes");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{className}.uc"), content);
    }
}
