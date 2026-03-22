using Shouldly;
using XCom2ConfigParser2.StructValidation;

namespace XCom2ConfigParser2.Tests.StructValidation;

public class VariableTypeResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _srcDir;

    public VariableTypeResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"var_resolver_{Guid.NewGuid()}");
        _srcDir = Path.Combine(_tempDir, "Src");
        Directory.CreateDirectory(_srcDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void VariableTypeResolver_Resolve_ValidClassFile_ReturnsVariableType()
    {
        CreateClassFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            var config int MyCount;
            var config SDLReplacement[] MyStructs;
            var config bool Enabled;
            """);

        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        var result = resolver.Resolve("TestPackage.TestClass", "MyStructs");

        result.Found.ShouldBeTrue();
        result.BaseType.ShouldBe("SDLReplacement");
        result.FullType.ShouldBe("SDLReplacement[]");
    }

    [Fact]
    public void VariableTypeResolver_Resolve_PrimitiveVariable_ReturnsPrimitiveType()
    {
        CreateClassFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            var config int MyCount;
            var config bool Enabled;
            """);

        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        var result = resolver.Resolve("TestPackage.TestClass", "MyCount");

        result.Found.ShouldBeTrue();
        result.BaseType.ShouldBe("int");
        result.FullType.ShouldBe("int");
    }

    [Fact]
    public void VariableTypeResolver_Resolve_NonExistentProperty_ReturnsNotFound()
    {
        CreateClassFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            var config int MyCount;
            """);

        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        var result = resolver.Resolve("TestPackage.TestClass", "NonExistent");

        result.Found.ShouldBeFalse();
    }

    [Fact]
    public void VariableTypeResolver_Resolve_NonExistentClass_ReturnsNotFound()
    {
        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        var result = resolver.Resolve("NonExistentPackage.NonExistentClass", "Property");

        result.Found.ShouldBeFalse();
    }

    [Fact]
    public void VariableTypeResolver_Resolve_InvalidSectionFormat_ReturnsNotFound()
    {
        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings();
        var resolver = new VariableTypeResolver(settings);

        var result = resolver.Resolve("InvalidSection", "Property");

        result.Found.ShouldBeFalse();
    }

    [Fact]
    public void VariableTypeResolver_Resolve_CaseInsensitiveProperty_FindsProperty()
    {
        CreateClassFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            var config int MyCount;
            """);

        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        var result = resolver.Resolve("TestPackage.TestClass", "mycount");

        result.Found.ShouldBeTrue();
    }

    [Fact]
    public void VariableTypeResolver_Resolve_NonConfigVar_IsNotFound()
    {
        CreateClassFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            var int NonConfigCount;
            """);

        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        var result = resolver.Resolve("TestPackage.TestClass", "NonConfigCount");
        result.Found.ShouldBeFalse("non-config vars cannot appear in ini files");
    }

    [Fact]
    public void VariableTypeResolver_Resolve_ArrayType_StripsArraySuffix()
    {
        CreateClassFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            var config array<SDLReplacement> MyStructs;
            """);

        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        var result = resolver.Resolve("TestPackage.TestClass", "MyStructs");

        result.Found.ShouldBeTrue();
        result.BaseType.ShouldBe("SDLReplacement");
    }

    private void CreateClassFile(string packageName, string className, string content)
    {
        var packageDir = Path.Combine(_srcDir, packageName, "Classes");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, $"{className}.uc"), content);
    }
}
