using Shouldly;
using XCom2ConfigParser2.Configuration;
using XCom2ConfigParser2.StructValidation;

namespace XCom2ConfigParser2.Tests.StructValidation;

public class StructDefinitionResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheDir;
    private readonly string _projectRoot;

    public StructDefinitionResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"struct_resolver_{Guid.NewGuid()}");
        _cacheDir = Path.Combine(_tempDir, "cache");
        _projectRoot = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(_cacheDir);
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void StructDefinitionResolver_Resolve_CacheHit_ReturnsCachedStruct()
    {
        var cache = new StructCache(_cacheDir);
        var tempFile = Path.Combine(_tempDir, "test.uc");
        File.WriteAllText(tempFile, "struct TestStruct { var int Field1; };");

        var cachedDef = new CachedStructDef
        {
            StructName = "TestStruct",
            SourceFile = tempFile,
            SourceHash = ComputeHash(tempFile),
            LastIndexed = DateTime.UtcNow,
            Fields = new List<StructField> { new() { Name = "Field1", TypeName = "int" } },
            NestedStructs = new List<string>(),
            ResolvedNestedStructs = true
        };
        cache.Save(cachedDef);

        var settings = new ParserSettings();
        var resolver = new StructDefinitionResolver(settings, cache);

        var result = resolver.Resolve("TestStruct");

        result.Found.ShouldBeTrue();
        result.ResolutionSource.ShouldBe("Cache");
        result.StructDef.ShouldNotBeNull();
        result.StructDef!.StructName.ShouldBe("TestStruct");
    }

    [Fact]
    public void StructDefinitionResolver_Resolve_ProjectSource_FindsStruct()
    {
        var cache = new StructCache(_cacheDir);
        CreateStructFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            struct TestStruct
            {
                var int Count;
                var bool Enabled;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_projectRoot, "Src") };
        var resolver = new StructDefinitionResolver(settings, cache);

        var result = resolver.Resolve("TestStruct");

        result.Found.ShouldBeTrue();
        result.ResolutionSource.ShouldBe("LocalSrc");
        result.StructDef.ShouldNotBeNull();
        result.StructDef!.Fields.Count.ShouldBe(2);
    }

    [Fact]
    public void StructDefinitionResolver_Resolve_NonExistentStruct_ReturnsNotFound()
    {
        var cache = new StructCache(_cacheDir);
        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_projectRoot, "Src") };
        Directory.CreateDirectory(Path.Combine(_projectRoot, "Src"));
        var resolver = new StructDefinitionResolver(settings, cache);

        var result = resolver.Resolve("NonExistentStruct");

        result.Found.ShouldBeFalse();
    }

    [Fact]
    public void StructDefinitionResolver_Resolve_NestedStructs_RecursiveResolution()
    {
        var cache = new StructCache(_cacheDir);
        CreateStructFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            struct InnerStruct
            {
                var int Value;
            };
            
            struct OuterStruct
            {
                var InnerStruct Inner;
                var int Count;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_projectRoot, "Src") };
        var resolver = new StructDefinitionResolver(settings, cache);

        var result = resolver.Resolve("OuterStruct");

        result.Found.ShouldBeTrue();
        result.StructDef.ShouldNotBeNull();
        result.StructDef!.NestedStructs.ShouldContain("InnerStruct");
        result.StructDef.ResolvedNestedStructs.ShouldBeTrue();
    }

    [Fact]
    public void StructDefinitionResolver_Resolve_CircularReference_DoesNotStackOverflow()
    {
        var cache = new StructCache(_cacheDir);
        CreateStructFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            struct NodeA
            {
                var NodeB Next;
            };
            
            struct NodeB
            {
                var NodeA Prev;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_projectRoot, "Src") };
        var resolver = new StructDefinitionResolver(settings, cache);

        // should not throw despite circular reference
        var result = resolver.Resolve("NodeA");

        result.Found.ShouldBeTrue();
    }

    [Fact]
    public void StructDefinitionResolver_Resolve_CachesResult_AfterSuccessfulResolution()
    {
        var cache = new StructCache(_cacheDir);
        CreateStructFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            struct TestStruct
            {
                var int Count;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_projectRoot, "Src") };
        var resolver = new StructDefinitionResolver(settings, cache);

        resolver.Resolve("TestStruct");

        var cacheFile = Path.Combine(_cacheDir, "TestStruct.json");
        File.Exists(cacheFile).ShouldBeTrue();
    }

    [Fact]
    public void StructDefinitionResolver_Resolve_StructWithArrayFields_ParsesCorrectly()
    {
        var cache = new StructCache(_cacheDir);
        CreateStructFile("TestPackage", "TestClass", """
            class TestClass extends Object;
            
            struct TestStruct
            {
                var int[] Numbers;
                var string[] Names;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_projectRoot, "Src") };
        var resolver = new StructDefinitionResolver(settings, cache);

        var result = resolver.Resolve("TestStruct");

        result.Found.ShouldBeTrue();
        result.StructDef!.Fields.Count.ShouldBe(2);
        result.StructDef.Fields[0].TypeName.ShouldBe("int[]");
        result.StructDef.Fields[1].TypeName.ShouldBe("string[]");
    }

    private void CreateStructFile(string packageName, string className, string content)
    {
        var packageDir = Path.Combine(_projectRoot, "Src", packageName, "Classes");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, $"{className}.uc"), content);
    }

    private static string ComputeHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
