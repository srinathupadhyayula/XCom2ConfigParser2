using Shouldly;
using X2ModCompiler.Core.Parser;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Core.Tests.Parser;

/// <summary>
/// Tests for <see cref="UnrealScriptParser"/> — the canonical, unified .uc file parser.
/// </summary>
public class UnrealScriptParserTests : TestBase
{
    // ------------------------------------------------------------------------
    // ParseConfigVariables
    // ------------------------------------------------------------------------

    [Fact]
    public async Task ParseConfigVariables_SimpleConfigVar_ReturnsVariable()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "var config int MyCount;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.ShouldContain(v => v.Name == "MyCount" && v.BaseType == "int");
    }

    [Fact]
    public async Task ParseConfigVariables_ArrayConfigVar_ReturnsIsArray()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "var config array<SDLReplacement> MyStructs;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        var v = result.ShouldHaveSingleItem();
        v.Name.ShouldBe("MyStructs");
        v.BaseType.ShouldBe("SDLReplacement");
        v.IsArray.ShouldBeTrue();
        v.TypeName.ShouldBe("SDLReplacement[]");
    }

    [Fact]
    public async Task ParseConfigVariables_NonConfigVar_IsIgnored()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "var int NonConfigCount;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseConfigVariables_StopsAtFirstFunction()
    {
        await using var temp = new TempDirectory();
        var content = """
            var config int BeforeFunction;

            function DoSomething()
            {
                var config int InsideFunction;
            }
            """;
        var file = await WriteAsync(temp, content);
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("BeforeFunction");
    }

    [Fact]
    public async Task ParseConfigVariables_WithGroupModifier_ReturnsVariable()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "var(Group) config int MyCount;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.ShouldContain(v => v.Name == "MyCount" && v.BaseType == "int");
    }

    [Fact]
    public async Task ParseConfigVariables_WithBracketArrayForm_ReturnsIsArray()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "var config int MyItems[];");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        var v = result.ShouldHaveSingleItem();
        v.IsArray.ShouldBeTrue();
        v.BaseType.ShouldBe("int");
    }

    [Fact]
    public async Task ParseConfigVariables_MultipleVariablesOnOneLine_ReturnsAll()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "var config int A, B, C;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain(v => v.Name == "A" && v.BaseType == "int");
        result.ShouldContain(v => v.Name == "B" && v.BaseType == "int");
        result.ShouldContain(v => v.Name == "C" && v.BaseType == "int");
    }

    [Fact]
    public async Task ParseConfigVariables_MultipleVariablesWithBrackets_ReturnsCorrectIsArray()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "var config int A, B[10], C;");
        var result = UnrealScriptParser.ParseConfigVariables(file);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        
        var a = result.First(v => v.Name == "A");
        a.IsArray.ShouldBeFalse();

        var b = result.First(v => v.Name == "B");
        b.IsArray.ShouldBeTrue();
        b.TypeName.ShouldBe("int[]");

        var c = result.First(v => v.Name == "C");
        c.IsArray.ShouldBeFalse();
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
    public async Task ParseStructs_SimpleStruct_ReturnsStruct()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, """
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
    public async Task ParseStructs_NativeStruct_ReturnsStruct()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, """
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
    public async Task ParseStructs_MultipleFieldsOnOneLine_ReturnsAll()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, """
            struct MyStruct
            {
                var int X, Y;
            };
            """);
        var result = UnrealScriptParser.ParseStructs(file);
        var s = result.ShouldHaveSingleItem();
        s.Fields.Count.ShouldBe(2);
        s.Fields.ShouldContain(f => f.Name == "X" && f.BaseType == "int");
        s.Fields.ShouldContain(f => f.Name == "Y" && f.BaseType == "int");
    }

    [Fact]
    public async Task ParseStructs_MultipleStructs_ReturnsAll()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, """
            struct StructA { var int X; };
            struct StructB { var bool Y; };
            """);
        var result = UnrealScriptParser.ParseStructs(file);
        result.Count.ShouldBe(2);
        result.ShouldContain(s => s.Name == "StructA");
        result.ShouldContain(s => s.Name == "StructB");
    }

    [Fact]
    public async Task ParseStructs_NoStructs_ReturnsEmpty()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "class TestClass extends Object;");
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
    public async Task FindStruct_ExistingStruct_ReturnsStruct()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, """
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
    public async Task FindStruct_NonExistentStruct_ReturnsNull()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "class TestClass extends Object;");
        var result = UnrealScriptParser.FindStruct(file, "NonExistentStruct");
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindStruct_CaseInsensitive_FindsStruct()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, """
            struct MyStruct { var int X; };
            """);
        var result = UnrealScriptParser.FindStruct(file, "mystruct");
        result.ShouldNotBeNull();
    }

    // ------------------------------------------------------------------------
    // FileContainsStruct
    // ------------------------------------------------------------------------

    [Fact]
    public async Task FileContainsStruct_StructPresent_ReturnsTrue()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "struct MyStruct { var int X; };");
        UnrealScriptParser.FileContainsStruct(file, "MyStruct").ShouldBeTrue();
    }

    [Fact]
    public async Task FileContainsStruct_StructAbsent_ReturnsFalse()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "class TestClass extends Object;");
        UnrealScriptParser.FileContainsStruct(file, "NonExistentStruct").ShouldBeFalse();
    }

    [Fact]
    public async Task FileContainsStruct_NativeStruct_ReturnsTrue()
    {
        await using var temp = new TempDirectory();
        var file = await WriteAsync(temp, "struct native MyNativeStruct { var int X; };");
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

    private static async Task<string> WriteAsync(TempDirectory temp, string content)
    {
        var path = Path.Combine(temp.Path, $"{Guid.NewGuid()}.uc");
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
