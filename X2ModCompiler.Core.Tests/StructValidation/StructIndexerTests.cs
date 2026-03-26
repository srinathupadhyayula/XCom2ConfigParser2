using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using X2ModCompiler.Core.Configuration;
using X2ModCompiler.Core.Parser;
using X2ModCompiler.Core.StructValidation;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Core.Tests.Validation;

/// <summary>
/// Tests for <see cref="StructIndexer"/> — pre-indexes all structs from configured source directories.
/// </summary>
public class StructIndexerTests : TestBase
{
    private readonly ILogger<StructIndexer> _logger;

    public StructIndexerTests()
    {
        _logger = Substitute.For<ILogger<StructIndexer>>();
    }

    [Fact]
    public async Task StructIndexer_IndexAll_EmptyDirectory_ReturnsZeroStructs()
    {
        await using var temp = new TempDirectory();
        var cacheDir = Path.Combine(temp.Path, "cache");
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(srcDir);

        var settings = new ParserSettings { LocalSrcRoot = srcDir };
        var cache = new StructCache(cacheDir);
        var modSrcCache = new ModSrcPathCache(settings, NullLogger<ModSrcPathCache>.Instance);
        var indexer = new StructIndexer(settings, cache, modSrcCache, _logger);

        var result = indexer.IndexAll();

        result.StructsFound.ShouldBe(0);
        result.FilesScanned.ShouldBe(0);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task StructIndexer_IndexAll_SingleStruct_IndexesCorrectly()
    {
        await using var temp = new TempDirectory();
        var cacheDir = Path.Combine(temp.Path, "cache");
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(srcDir);

        CreateUcFile(srcDir, "TestPackage", "TestClass", """
            struct MyStruct
            {
                var int Count;
                var bool Enabled;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = srcDir };
        var cache = new StructCache(cacheDir);
        var modSrcCache = new ModSrcPathCache(settings, NullLogger<ModSrcPathCache>.Instance);
        var indexer = new StructIndexer(settings, cache, modSrcCache, _logger);

        var result = indexer.IndexAll();

        result.StructsFound.ShouldBeGreaterThan(0);
        result.FilesScanned.ShouldBeGreaterThan(0);

        // Struct should now be in cache
        cache.TryGet("MyStruct", out var cached).ShouldBeTrue();
        cached.ShouldNotBeNull();
        cached!.Fields.Count.ShouldBe(2);
    }

    [Fact]
    public async Task StructIndexer_IndexAll_MultipleStructsInOneFile_IndexesAll()
    {
        await using var temp = new TempDirectory();
        var cacheDir = Path.Combine(temp.Path, "cache");
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(srcDir);

        CreateUcFile(srcDir, "TestPackage", "TestClass", """
            struct StructA { var int X; };
            struct StructB { var bool Y; };
            struct StructC { var float Z; };
            """);

        var settings = new ParserSettings { LocalSrcRoot = srcDir };
        var cache = new StructCache(cacheDir);
        var modSrcCache = new ModSrcPathCache(settings, NullLogger<ModSrcPathCache>.Instance);
        var indexer = new StructIndexer(settings, cache, modSrcCache, _logger);

        var result = indexer.IndexAll();

        result.StructsFound.ShouldBe(3);
        cache.TryGet("StructA", out _).ShouldBeTrue();
        cache.TryGet("StructB", out _).ShouldBeTrue();
        cache.TryGet("StructC", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task StructIndexer_IndexAll_NoneStructFile_SkipsFile()
    {
        await using var temp = new TempDirectory();
        var cacheDir = Path.Combine(temp.Path, "cache");
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(srcDir);

        CreateUcFile(srcDir, "TestPackage", "TestClass", "class TestClass extends Object;\nvar config int Count;");

        var settings = new ParserSettings { LocalSrcRoot = srcDir };
        var cache = new StructCache(cacheDir);
        var modSrcCache = new ModSrcPathCache(settings, NullLogger<ModSrcPathCache>.Instance);
        var indexer = new StructIndexer(settings, cache, modSrcCache, _logger);

        var result = indexer.IndexAll();

        result.StructsFound.ShouldBe(0);
        result.FilesScanned.ShouldBeGreaterThan(0); // file was scanned, just no structs
    }

    [Fact]
    public async Task StructIndexer_IndexAll_ReportsProgressCallback()
    {
        await using var temp = new TempDirectory();
        var cacheDir = Path.Combine(temp.Path, "cache");
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(srcDir);

        CreateUcFile(srcDir, "TestPackage", "TestClass", "struct S { var int X; };");

        var settings = new ParserSettings { LocalSrcRoot = srcDir };
        var cache = new StructCache(cacheDir);
        var modSrcCache = new ModSrcPathCache(settings, NullLogger<ModSrcPathCache>.Instance);
        var indexer = new StructIndexer(settings, cache, modSrcCache, _logger);

        var progressReports = new List<IndexingProgress>();
        var progress = new Progress<IndexingProgress>(p => progressReports.Add(p));

        indexer.IndexAll(progress);

        // Give the Progress<T> callbacks time to fire (they are async)
        await Task.Delay(100);

        progressReports.ShouldNotBeEmpty();
        progressReports.All(p => p.FilesProcessed > 0).ShouldBeTrue();
    }

    [Fact]
    public async Task StructIndexer_IndexAll_InaccessibleDirectory_DoesNotThrow()
    {
        await using var temp = new TempDirectory();
        var cacheDir = Path.Combine(temp.Path, "cache");
        Directory.CreateDirectory(cacheDir);

        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(temp.Path, "doesnotexist") };
        var cache = new StructCache(cacheDir);
        var modSrcCache = new ModSrcPathCache(settings, NullLogger<ModSrcPathCache>.Instance);
        var indexer = new StructIndexer(settings, cache, modSrcCache, _logger);

        // Should not throw
        var result = indexer.IndexAll();

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task StructIndexer_IndexAll_SkipsAlreadyCachedStructs()
    {
        await using var temp = new TempDirectory();
        var cacheDir = Path.Combine(temp.Path, "cache");
        var srcDir = Path.Combine(temp.Path, "Src");
        Directory.CreateDirectory(cacheDir);
        Directory.CreateDirectory(srcDir);

        var ucFile = CreateUcFile(srcDir, "TestPackage", "TestClass", "struct CachedStruct { var int X; };");
        var hash = ComputeHash(ucFile);

        var settings = new ParserSettings { LocalSrcRoot = srcDir };
        var cache = new StructCache(cacheDir);

        // Pre-populate cache with this struct
        cache.Save(new CachedStructDef
        {
            StructName = "CachedStruct",
            SourceFile = ucFile,
            SourceHash = hash,
            LastIndexed = DateTime.UtcNow,
            Fields = new List<StructField>(),
            NestedStructs = new List<string>(),
            ResolvedNestedStructs = true
        });

        var modSrcCache = new ModSrcPathCache(settings, NullLogger<ModSrcPathCache>.Instance);
        var indexer = new StructIndexer(settings, cache, modSrcCache, _logger);

        var result = indexer.IndexAll();

        // Struct was already cached, so it should not be re-indexed
        result.StructsFound.ShouldBe(0);
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private string CreateUcFile(string srcDir, string packageName, string className, string content)
    {
        var dir = Path.Combine(srcDir, packageName, "Classes");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{className}.uc");
        File.WriteAllText(path, content);
        return path;
    }

    private static string ComputeHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
