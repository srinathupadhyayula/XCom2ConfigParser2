using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using X2ModCompiler.Core.Configuration;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Core.StructValidation;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Core.Tests.Validation;

public class StructDefinitionResolverTests : TestBase
{
    private readonly ILogger<StructDefinitionResolver> _logger = NullLogger<StructDefinitionResolver>.Instance;
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    public StructDefinitionResolverTests()
    {
    }

    [Fact]
    public async Task StructDefinitionResolver_Resolve_CacheHit_ReturnsCachedStruct()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
        var tempFile = Path.Combine(temp.Path, "test.uc");
        await File.WriteAllTextAsync(tempFile, "struct TestStruct { var int Field1; };", TestContext.Current.CancellationToken);

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
        var resolver = new StructDefinitionResolver(settings, cache, _loggerFactory);

        var result = resolver.Resolve("TestStruct");

        result.Found.ShouldBeTrue();
        result.ResolutionSource.ShouldBe("Cache");
        result.StructDef.ShouldNotBeNull();
        result.StructDef!.StructName.ShouldBe("TestStruct");
    }

    [Fact]
    public async Task StructDefinitionResolver_Resolve_ProjectSource_FindsStruct()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(Path.Combine(temp.Path, "cache"));
        var projectRoot = Path.Combine(temp.Path, "project");
        var localSrcRoot = Path.Combine(projectRoot, "Src");
        
        CreateStructFile(localSrcRoot, "TestPackage", "TestClass", """
            class TestClass extends Object;
            
            struct TestStruct
            {
                var int Count;
                var bool Enabled;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = localSrcRoot };
        var resolver = new StructDefinitionResolver(settings, cache, _loggerFactory);

        var result = resolver.Resolve("TestStruct");

        result.Found.ShouldBeTrue();
        result.ResolutionSource.ShouldBe("LocalSrc");
        result.StructDef.ShouldNotBeNull();
        result.StructDef!.Fields.Count.ShouldBe(2);
    }

    [Fact]
    public async Task StructDefinitionResolver_Resolve_NonExistentStruct_ReturnsNotFound()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
        var settings = new ParserSettings { LocalSrcRoot = temp.Path };
        var resolver = new StructDefinitionResolver(settings, cache, _loggerFactory);

        var result = resolver.Resolve("NonExistentStruct");

        result.Found.ShouldBeFalse();
    }

    [Fact]
    public async Task StructDefinitionResolver_Resolve_NestedStructs_RecursiveResolution()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(Path.Combine(temp.Path, "cache"));
        var localSrcRoot = Path.Combine(temp.Path, "Src");

        CreateStructFile(localSrcRoot, "TestPackage", "TestClass", """
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

        var settings = new ParserSettings { LocalSrcRoot = localSrcRoot };
        var resolver = new StructDefinitionResolver(settings, cache, _loggerFactory);

        var result = resolver.Resolve("OuterStruct");

        result.Found.ShouldBeTrue();
        result.StructDef.ShouldNotBeNull();
        result.StructDef!.NestedStructs.ShouldContain("InnerStruct");
        result.StructDef.ResolvedNestedStructs.ShouldBeTrue();
    }

    [Fact]
    public async Task StructDefinitionResolver_Resolve_CircularReference_DoesNotStackOverflow()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(Path.Combine(temp.Path, "cache"));
        var localSrcRoot = Path.Combine(temp.Path, "Src");

        CreateStructFile(localSrcRoot, "TestPackage", "TestClass", """
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

        var settings = new ParserSettings { LocalSrcRoot = localSrcRoot };
        var resolver = new StructDefinitionResolver(settings, cache, _loggerFactory);

        var result = resolver.Resolve("NodeA");

        result.Found.ShouldBeTrue();
    }

    [Fact]
    public async Task StructDefinitionResolver_Resolve_CachesResult_AfterSuccessfulResolution()
    {
        await using var temp = new TempDirectory();
        var cacheDir = Path.Combine(temp.Path, "cache");
        var cache = new StructCache(cacheDir);
        var localSrcRoot = Path.Combine(temp.Path, "Src");

        CreateStructFile(localSrcRoot, "TestPackage", "TestClass", """
            class TestClass extends Object;
            
            struct TestStruct
            {
                var int Count;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = localSrcRoot };
        var resolver = new StructDefinitionResolver(settings, cache, _loggerFactory);

        resolver.Resolve("TestStruct");

        var cacheFile = Path.Combine(temp.Path, "cache", "structsmap", "TestStruct.mempack");
        File.Exists(cacheFile).ShouldBeTrue();
    }

    [Fact]
    public async Task StructDefinitionResolver_Resolve_StructWithArrayFields_ParsesCorrectly()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(Path.Combine(temp.Path, "cache"));
        var localSrcRoot = Path.Combine(temp.Path, "Src");

        CreateStructFile(localSrcRoot, "TestPackage", "TestClass", """
            class TestClass extends Object;
            
            struct TestStruct
            {
                var int[] Numbers;
                var string[] Names;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = localSrcRoot };
        var resolver = new StructDefinitionResolver(settings, cache, _loggerFactory);

        var result = resolver.Resolve("TestStruct");

        result.Found.ShouldBeTrue();
        result.StructDef!.Fields.Count.ShouldBe(2);
        result.StructDef.Fields[0].TypeName.ShouldBe("int[]");
        result.StructDef.Fields[1].TypeName.ShouldBe("string[]");
    }

    private void CreateStructFile(string localSrcRoot, string packageName, string className, string content)
    {
        var packageDir = Path.Combine(localSrcRoot, packageName, "Classes");
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
