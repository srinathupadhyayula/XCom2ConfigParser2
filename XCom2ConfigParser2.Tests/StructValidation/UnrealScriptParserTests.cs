using Shouldly;
using XCom2ConfigParser2.StructValidation;

namespace XCom2ConfigParser2.Tests.StructValidation;

/// <summary>
/// Tests for <see cref="UnrealScriptParser"/> — the canonical, unified .uc file parser.
/// </summary>
public class UnrealScriptParserTests : IDisposable
{
    private readonly string _tempDir;

    public UnrealScriptParserTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"us_parser_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    // ------------------------------------------------------------------------
    // ParseConfigVariables
    // ------------------------------------------------------------------------

    [Fact]
    public void ParseConfigVariables_SimpleConfigVar_ReturnsVariable()
    {
        var file = Write("var config int MyCount;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.ShouldContain(v => v.Name == "MyCount" && v.BaseType == "int");
    }

    [Fact]
    public void ParseConfigVariables_ArrayConfigVar_ReturnsIsArray()
    {
        var file = Write("var config array<SDLReplacement> MyStructs;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        var v = result.ShouldHaveSingleItem();
        v.Name.ShouldBe("MyStructs");
        v.BaseType.ShouldBe("SDLReplacement");
        v.IsArray.ShouldBeTrue();
        v.TypeName.ShouldBe("SDLReplacement[]");
    }

    [Fact]
    public void ParseConfigVariables_NonConfigVar_IsIgnored()
    {
        var file = Write("var int NonConfigCount;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseConfigVariables_StopsAtFirstFunction()
    {
        var content = """
            var config int BeforeFunction;

            function DoSomething()
            {
                var config int InsideFunction;
            }
            """;
        var file = Write(content);
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("BeforeFunction");
    }

    [Fact]
    public void ParseConfigVariables_WithGroupModifier_ReturnsVariable()
    {
        var file = Write("var(Group) config int MyCount;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.ShouldContain(v => v.Name == "MyCount" && v.BaseType == "int");
    }

    [Fact]
    public void ParseConfigVariables_WithBracketArrayForm_ReturnsIsArray()
    {
        var file = Write("var config int MyItems[];");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        var v = result.ShouldHaveSingleItem();
        v.IsArray.ShouldBeTrue();
        v.BaseType.ShouldBe("int");
    }

    [Fact]
    public void ParseConfigVariables_MissingFile_ReturnsNull()
    {
        var result = UnrealScriptParser.ParseConfigVariables("/nonexistent/path/file.uc");
        result.ShouldBeNull();
    }

    // ------------------------------------------------------------------------
    // ParseStructs
    // ------------------------------------------------------------------------

    [Fact]
    public void ParseStructs_SimpleStruct_ReturnsStruct()
    {
        var file = Write("""
            struct MyStruct
            {
                var int Count;
                var bool Enabled;
            };
            """);
        var result = UnrealScriptParser.ParseStructs(file);
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("MyStruct");
        result[0].Fields.Count.ShouldBe(2);
    }

    [Fact]
    public void ParseStructs_NativeStruct_ReturnsStruct()
    {
        var file = Write("""
            struct native MyNativeStruct
            {
                var int Value;
            };
            """);
        var result = UnrealScriptParser.ParseStructs(file);
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("MyNativeStruct");
    }

    [Fact]
    public void ParseStructs_MultipleStructs_ReturnsAll()
    {
        var file = Write("""
            struct StructA { var int X; };
            struct StructB { var bool Y; };
            """);
        var result = UnrealScriptParser.ParseStructs(file);
        result.Count.ShouldBe(2);
        result.ShouldContain(s => s.Name == "StructA");
        result.ShouldContain(s => s.Name == "StructB");
    }

    [Fact]
    public void ParseStructs_NoStructs_ReturnsEmpty()
    {
        var file = Write("class TestClass extends Object;");
        var result = UnrealScriptParser.ParseStructs(file);
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseStructs_MissingFile_ReturnsEmpty()
    {
        var result = UnrealScriptParser.ParseStructs("/nonexistent/path/file.uc");
        result.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------
    // FindStruct
    // ------------------------------------------------------------------------

    [Fact]
    public void FindStruct_ExistingStruct_ReturnsStruct()
    {
        var file = Write("""
            struct MyStruct
            {
                var int Count;
            };
            """);
        var result = UnrealScriptParser.FindStruct(file, "MyStruct");
        result.ShouldNotBeNull();
        result!.Name.ShouldBe("MyStruct");
        result.Fields.Count.ShouldBe(1);
    }

    [Fact]
    public void FindStruct_NonExistentStruct_ReturnsNull()
    {
        var file = Write("class TestClass extends Object;");
        var result = UnrealScriptParser.FindStruct(file, "NonExistentStruct");
        result.ShouldBeNull();
    }

    [Fact]
    public void FindStruct_CaseInsensitive_FindsStruct()
    {
        var file = Write("""
            struct MyStruct { var int X; };
            """);
        var result = UnrealScriptParser.FindStruct(file, "mystruct");
        result.ShouldNotBeNull();
    }

    // ------------------------------------------------------------------------
    // FileContainsStruct
    // ------------------------------------------------------------------------

    [Fact]
    public void FileContainsStruct_StructPresent_ReturnsTrue()
    {
        var file = Write("struct MyStruct { var int X; };");
        UnrealScriptParser.FileContainsStruct(file, "MyStruct").ShouldBeTrue();
    }

    [Fact]
    public void FileContainsStruct_StructAbsent_ReturnsFalse()
    {
        var file = Write("class TestClass extends Object;");
        UnrealScriptParser.FileContainsStruct(file, "NonExistentStruct").ShouldBeFalse();
    }

    [Fact]
    public void FileContainsStruct_NativeStruct_ReturnsTrue()
    {
        var file = Write("struct native MyNativeStruct { var int X; };");
        UnrealScriptParser.FileContainsStruct(file, "MyNativeStruct").ShouldBeTrue();
    }

    // ------------------------------------------------------------------------
    // GetDeclarationSection
    // ------------------------------------------------------------------------

    [Fact]
    public void GetDeclarationSection_ReturnsSectionBeforeFunction()
    {
        var content = "var config int X;\nfunction Foo() { }";
        var section = UnrealScriptParser.GetDeclarationSection(content);
        section.ShouldContain("var config int X;");
        section.ShouldNotContain("function Foo");
    }

    [Fact]
    public void GetDeclarationSection_NoFunction_ReturnsWholeContent()
    {
        var content = "var config int X;\nvar config int Y;";
        var section = UnrealScriptParser.GetDeclarationSection(content);
        section.ShouldBe(content);
    }

    // ------------------------------------------------------------------------
    // KnownPrimitives
    // ------------------------------------------------------------------------

    [Theory]
    [InlineData("int")]
    [InlineData("bool")]
    [InlineData("float")]
    [InlineData("string")]
    [InlineData("name")]
    [InlineData("byte")]
    [InlineData("vector")]
    [InlineData("rotator")]
    public void KnownPrimitives_ContainsCoreTypes(string type)
    {
        UnrealScriptParser.KnownPrimitives.Contains(type).ShouldBeTrue();
    }

    [Fact]
    public void KnownPrimitives_IsCaseInsensitive()
    {
        UnrealScriptParser.KnownPrimitives.Contains("INT").ShouldBeTrue();
        UnrealScriptParser.KnownPrimitives.Contains("Bool").ShouldBeTrue();
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private string Write(string content)
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid()}.uc");
        File.WriteAllText(path, content);
        return path;
    }
}
