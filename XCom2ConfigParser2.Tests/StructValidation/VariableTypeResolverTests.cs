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

    [Fact]
    public void VariableTypeResolver_Resolve_ObjectClassFormat_ReturnsVariableType()
    {
        // Class is in some package
        CreateClassFile("XComGame", "X2CharacterTemplate", """
            class X2CharacterTemplate extends Object;
            var config array<name> SupportedFollowers;
            """);

        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        // Section uses Object Class format: [Specialist X2CharacterTemplate]
        var result = resolver.Resolve("Specialist X2CharacterTemplate", "SupportedFollowers");

        result.Found.ShouldBeTrue("Should find class globally when space-separated");
        result.BaseType.ShouldBe("name");
    }

    [Fact]
    public void VariableTypeResolver_Resolve_ClassNameOnlyFormat_ReturnsVariableType()
    {
        // Class is in some package
        CreateClassFile("MyMod", "MyCustomClass", """
            class MyCustomClass extends Object;
            var config int MyValue;
            """);

        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        // Section uses just Class format: [MyCustomClass]
        var result = resolver.Resolve("MyCustomClass", "MyValue");

        result.Found.ShouldBeTrue("Should find class globally when no separator is used");
        result.BaseType.ShouldBe("int");
    }

    [Fact]
    public void VariableTypeResolver_Resolve_InheritedVariable_ReturnsVariableType()
    {
        // Parent class in XComGame
        CreateClassFile("XComGame", "X2ItemTemplate", """
            class X2ItemTemplate extends Object;
            var config StrategyCost Cost;
            """);

        // Child class in same or different package
        CreateClassFile("XComGame", "X2EquipmentTemplate", """
            class X2EquipmentTemplate extends X2ItemTemplate;
            var config int Weight;
            """);

        var settings = new global::XCom2ConfigParser2.Configuration.ParserSettings { LocalSrcRoot = _srcDir };
        var resolver = new VariableTypeResolver(settings);

        // Resolve inherited variable 'Cost' in 'X2EquipmentTemplate' context
        var result = resolver.Resolve("XComGame.X2EquipmentTemplate", "Cost");

        result.Found.ShouldBeTrue("Should find inherited variable in parent class");
        result.BaseType.ShouldBe("StrategyCost");
    }

    private void CreateClassFile(string packageName, string className, string content)
    {
        var packageDir = Path.Combine(_srcDir, packageName, "Classes");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, $"{className}.uc"), content);
    }
}
