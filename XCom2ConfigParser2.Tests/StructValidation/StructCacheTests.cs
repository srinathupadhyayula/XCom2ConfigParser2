using Shouldly;
using XCom2ConfigParser2.StructValidation;

namespace XCom2ConfigParser2.Tests.StructValidation;

public class StructCacheTests : IDisposable
{
    private readonly string _cacheDir;

    public StructCacheTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), $"struct_cache_{Guid.NewGuid()}");
        Directory.CreateDirectory(_cacheDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, true);
    }

    [Fact]
    public void StructCache_Constructor_CreatesCacheDirectory()
    {
        string newCacheDir = Path.Combine(_cacheDir, "new_cache");
        var cache = new StructCache(newCacheDir);
        Directory.Exists(newCacheDir).ShouldBeTrue();
    }

    [Fact]
    public void StructCache_TryGet_NonExistentCache_ReturnsFalse()
    {
        var cache = new StructCache(_cacheDir);
        var result = cache.TryGet("NonExistentStruct", out var cached);
        result.ShouldBeFalse();
        cached.ShouldBeNull();
    }

    [Fact]
    public void StructCache_Save_ThenTryGet_ReturnsCachedStruct()
    {
        var cache = new StructCache(_cacheDir);
        var tempFile = Path.Combine(_cacheDir, "test.uc");
        File.WriteAllText(tempFile, "test content");
        string hash = ComputeHash(tempFile);

        var def = new CachedStructDef
        {
            StructName = "TestStruct",
            SourceFile = tempFile,
            SourceHash = hash,
            LastIndexed = DateTime.UtcNow,
            Fields = new List<StructField> { new() { Name = "Field1", TypeName = "int" } },
            NestedStructs = new List<string>(),
            ResolvedNestedStructs = true
        };

        cache.Save(def);

        var cacheFile = Path.Combine(_cacheDir, "TestStruct.json");
        File.Exists(cacheFile).ShouldBeTrue();

        var result = cache.TryGet("TestStruct", out var cached);
        result.ShouldBeTrue("cache should be valid");
        cached.ShouldNotBeNull();
        cached!.StructName.ShouldBe("TestStruct");
        cached.SourceFile.ShouldBe(tempFile);
    }

    [Fact]
    public void StructCache_TryGet_ExpiredCache_ReturnsFalse()
    {
        var cache = new StructCache(_cacheDir);
        var def = new CachedStructDef
        {
            StructName = "ExpiredStruct",
            SourceFile = "Src/Test/Classes/Test.uc",
            SourceHash = "sha256:test123",
            LastIndexed = DateTime.UtcNow.AddDays(-2), // 2 days old
            Fields = new List<StructField>(),
            NestedStructs = new List<string>(),
            ResolvedNestedStructs = true
        };

        cache.Save(def);
        var result = cache.TryGet("ExpiredStruct", out var cached);
        result.ShouldBeFalse();
        cached.ShouldBeNull();
    }

    [Fact]
    public void StructCache_TryGet_SourceFileDeleted_ReturnsFalse()
    {
        var cache = new StructCache(_cacheDir);
        var tempFile = Path.Combine(_cacheDir, "temp.uc");
        File.WriteAllText(tempFile, "test content");

        var def = new CachedStructDef
        {
            StructName = "DeletedSourceStruct",
            SourceFile = tempFile,
            SourceHash = ComputeHash(tempFile),
            LastIndexed = DateTime.UtcNow,
            Fields = new List<StructField>(),
            NestedStructs = new List<string>(),
            ResolvedNestedStructs = true
        };

        cache.Save(def);
        File.Delete(tempFile);
        var result = cache.TryGet("DeletedSourceStruct", out var cached);
        result.ShouldBeFalse();
        cached.ShouldBeNull();
    }

    [Fact]
    public void StructCache_TryGet_SourceFileModified_ReturnsFalse()
    {
        var cache = new StructCache(_cacheDir);
        var tempFile = Path.Combine(_cacheDir, "temp.uc");
        File.WriteAllText(tempFile, "original content");

        var def = new CachedStructDef
        {
            StructName = "ModifiedStruct",
            SourceFile = tempFile,
            SourceHash = ComputeHash(tempFile),
            LastIndexed = DateTime.UtcNow,
            Fields = new List<StructField>(),
            NestedStructs = new List<string>(),
            ResolvedNestedStructs = true
        };

        cache.Save(def);
        File.WriteAllText(tempFile, "modified content");
        var result = cache.TryGet("ModifiedStruct", out var cached);
        result.ShouldBeFalse();
        cached.ShouldBeNull();
    }

    [Fact]
    public void StructCache_TryGet_ValidCache_ReturnsTrue()
    {
        var cache = new StructCache(_cacheDir);
        var tempFile = Path.Combine(_cacheDir, "valid.uc");
        File.WriteAllText(tempFile, "content");

        var def = new CachedStructDef
        {
            StructName = "ValidStruct",
            SourceFile = tempFile,
            SourceHash = ComputeHash(tempFile),
            LastIndexed = DateTime.UtcNow,
            Fields = new List<StructField>(),
            NestedStructs = new List<string>(),
            ResolvedNestedStructs = true
        };

        cache.Save(def);
        var result = cache.TryGet("ValidStruct", out var cached);
        result.ShouldBeTrue();
        cached.ShouldNotBeNull();
    }

    [Fact]
    public void StructCache_Save_ThenTryGet_ReturnsCorrectCacheFile()
    {
        var cache = new StructCache(_cacheDir);
        var def = new CachedStructDef
        {
            StructName = "TestStruct",
            SourceFile = "Src/Test/Classes/Test.uc",
            SourceHash = "sha256:test123",
            LastIndexed = DateTime.UtcNow,
            Fields = new List<StructField>(),
            NestedStructs = new List<string>(),
            ResolvedNestedStructs = true
        };

        cache.Save(def);

        string expectedPath = Path.Combine(_cacheDir, "TestStruct.json");
        File.Exists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public void StructCache_NegativeCache_SaveAndCheck()
    {
        var cache = new StructCache(_cacheDir);

        cache.SaveNotFound("MissingStruct");

        cache.IsKnownNotFound("MissingStruct").ShouldBeTrue();
        cache.IsKnownNotFound("OtherStruct").ShouldBeFalse();
    }

    [Fact]
    public void StructCache_ClearNegativeEntries_RemovesNegativeCache()
    {
        var cache = new StructCache(_cacheDir);
        cache.SaveNotFound("MissingStruct");
        cache.IsKnownNotFound("MissingStruct").ShouldBeTrue();

        cache.ClearNegativeEntries();

        cache.IsKnownNotFound("MissingStruct").ShouldBeFalse();
    }

    private static string ComputeHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
