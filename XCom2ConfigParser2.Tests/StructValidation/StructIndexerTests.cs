using Shouldly;
using XCom2ConfigParser2.Configuration;
using XCom2ConfigParser2.StructValidation;

namespace XCom2ConfigParser2.Tests.StructValidation;

/// <summary>
/// Tests for <see cref="StructIndexer"/> — pre-indexes all structs from configured source directories.
/// </summary>
public class StructIndexerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _cacheDir;
    private readonly string _srcDir;

    public StructIndexerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"struct_indexer_{Guid.NewGuid()}");
        _cacheDir = Path.Combine(_tempDir, "cache");
        _srcDir = Path.Combine(_tempDir, "Src");
        Directory.CreateDirectory(_cacheDir);
        Directory.CreateDirectory(_srcDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void StructIndexer_IndexAll_EmptyDirectory_ReturnsZeroStructs()
    {
        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var cache = new StructCache(_cacheDir);
        var modSrcCache = new ModSrcPathCache(settings);
        var indexer = new StructIndexer(settings, cache, modSrcCache);

        var result = indexer.IndexAll();

        result.StructsFound.ShouldBe(0);
        result.FilesScanned.ShouldBe(0);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void StructIndexer_IndexAll_SingleStruct_IndexesCorrectly()
    {
        CreateUcFile("TestPackage", "TestClass", """
            struct MyStruct
            {
                var int Count;
                var bool Enabled;
            };
            """);

        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var cache = new StructCache(_cacheDir);
        var modSrcCache = new ModSrcPathCache(settings);
        var indexer = new StructIndexer(settings, cache, modSrcCache);

        var result = indexer.IndexAll();

        result.StructsFound.ShouldBeGreaterThan(0);
        result.FilesScanned.ShouldBeGreaterThan(0);

        // Struct should now be in cache
        cache.TryGet("MyStruct", out var cached).ShouldBeTrue();
        cached.ShouldNotBeNull();
        cached!.Fields.Count.ShouldBe(2);
    }

    [Fact]
    public void StructIndexer_IndexAll_MultipleStructsInOneFile_IndexesAll()
    {
        CreateUcFile("TestPackage", "TestClass", """
            struct StructA { var int X; };
            struct StructB { var bool Y; };
            struct StructC { var float Z; };
            """);

        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var cache = new StructCache(_cacheDir);
        var modSrcCache = new ModSrcPathCache(settings);
        var indexer = new StructIndexer(settings, cache, modSrcCache);

        var result = indexer.IndexAll();

        result.StructsFound.ShouldBe(3);
        cache.TryGet("StructA", out _).ShouldBeTrue();
        cache.TryGet("StructB", out _).ShouldBeTrue();
        cache.TryGet("StructC", out _).ShouldBeTrue();
    }

    [Fact]
    public void StructIndexer_IndexAll_NoneStructFile_SkipsFile()
    {
        CreateUcFile("TestPackage", "TestClass", "class TestClass extends Object;\nvar config int Count;");

        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var cache = new StructCache(_cacheDir);
        var modSrcCache = new ModSrcPathCache(settings);
        var indexer = new StructIndexer(settings, cache, modSrcCache);

        var result = indexer.IndexAll();

        result.StructsFound.ShouldBe(0);
        result.FilesScanned.ShouldBeGreaterThan(0); // file was scanned, just no structs
    }

    [Fact]
    public void StructIndexer_IndexAll_ReportsProgressCallback()
    {
        CreateUcFile("TestPackage", "TestClass", "struct S { var int X; };");

        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var cache = new StructCache(_cacheDir);
        var modSrcCache = new ModSrcPathCache(settings);
        var indexer = new StructIndexer(settings, cache, modSrcCache);

        var progressReports = new List<IndexingProgress>();
        var progress = new Progress<IndexingProgress>(p => progressReports.Add(p));

        indexer.IndexAll(progress);

        // Give the Progress<T> callbacks time to fire (they are async)
        Thread.Sleep(50);

        progressReports.ShouldNotBeEmpty();
        progressReports.All(p => p.FilesProcessed > 0).ShouldBeTrue();
    }

    [Fact]
    public void StructIndexer_IndexAll_InaccessibleDirectory_DoesNotThrow()
    {
        var settings = new ParserSettings { LocalSrcRoot = Path.Combine(_tempDir, "doesnotexist") };
        var cache = new StructCache(_cacheDir);
        var modSrcCache = new ModSrcPathCache(settings);
        var indexer = new StructIndexer(settings, cache, modSrcCache);

        // Should not throw
        var result = indexer.IndexAll();

        result.ShouldNotBeNull();
    }

    [Fact]
    public void StructIndexer_IndexAll_SkipsAlreadyCachedStructs()
    {
        var ucFile = CreateUcFile("TestPackage", "TestClass", "struct CachedStruct { var int X; };");
        var hash = ComputeHash(ucFile);

        var settings = new ParserSettings { LocalSrcRoot = _srcDir };
        var cache = new StructCache(_cacheDir);

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

        var modSrcCache = new ModSrcPathCache(settings);
        var indexer = new StructIndexer(settings, cache, modSrcCache);

        var result = indexer.IndexAll();

        // Struct was already cached, so it should not be re-indexed
        result.StructsFound.ShouldBe(0);
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private string CreateUcFile(string packageName, string className, string content)
    {
        var dir = Path.Combine(_srcDir, packageName, "Classes");
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
