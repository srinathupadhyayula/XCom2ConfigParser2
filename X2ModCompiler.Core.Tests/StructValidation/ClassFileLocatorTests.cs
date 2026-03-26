using Shouldly;
using X2ModCompiler.Core.Configuration;
using X2ModCompiler.Core.StructValidation;

namespace X2ModCompiler.Core.Tests.StructValidation;

public class ClassFileLocatorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _projectRoot;

    public ClassFileLocatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"class_locator_{Guid.NewGuid()}");
        _projectRoot = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void ClassFileLocator_Locate_ProjectSource_FindsFile()
    {
        CreateClassFile(_projectRoot, "TestPackage", "TestClass");
        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_projectRoot, "Src") };
        var locator = new ClassFileLocator(settings);

        var result = locator.Locate("TestPackage", "TestClass");

        result.Found.ShouldBeTrue();
        result.Source.ShouldBe("LocalSrc");
        result.FilePath.ShouldEndWith("TestClass.uc");
    }

    [Fact]
    public void ClassFileLocator_Locate_NonExistentClass_ReturnsNotFound()
    {
        var settings = new ParserSettings();
        var locator = new ClassFileLocator(settings);

        var result = locator.Locate("NonExistent", "NonExistent");

        result.Found.ShouldBeFalse();
    }

    [Fact]
    public void ClassFileLocator_Locate_WithModPaths_SearchesModPaths()
    {
        string modSrc = Path.Combine(_tempDir, "mod", "Src");
        var modPackageDir = Path.Combine(modSrc, "ModPackage", "Classes");
        Directory.CreateDirectory(modPackageDir);
        File.WriteAllText(Path.Combine(modPackageDir, "ModClass.uc"), "class ModClass extends Object;");

        var settings = new ParserSettings
        {
            ModsCompiledAgainst = new List<string> { modSrc }
        };
        var locator = new ClassFileLocator(settings);

        var result = locator.Locate("ModPackage", "ModClass");

        result.Found.ShouldBeTrue();
        result.Source.ShouldBe("ModCompiledAgainst");
    }

    [Fact]
    public void ClassFileLocator_Locate_CaseInsensitive_FindsFile()
    {
        CreateClassFile(_projectRoot, "TestPackage", "TestClass");
        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_projectRoot, "Src") };
        var locator = new ClassFileLocator(settings);

        var result = locator.Locate("testpackage", "testclass");

        result.Found.ShouldBeTrue();
    }

    [Fact]
    public void ClassFileLocator_Locate_ClassesSubdirectory_FindsFile()
    {
        var classesDir = Path.Combine(_projectRoot, "Src", "TestPackage", "Classes");
        Directory.CreateDirectory(classesDir);
        File.WriteAllText(Path.Combine(classesDir, "TestClass.uc"), "class TestClass extends Object;");

        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_projectRoot, "Src") };
        var locator = new ClassFileLocator(settings);

        var result = locator.Locate("TestPackage", "TestClass");

        result.Found.ShouldBeTrue();
    }

    private void CreateClassFile(string root, string packageName, string className)
    {
        var classesDir = Path.Combine(root, "Src", packageName, "Classes");
        Directory.CreateDirectory(classesDir);
        File.WriteAllText(Path.Combine(classesDir, $"{className}.uc"), $"class {className} extends Object;");
    }
}
