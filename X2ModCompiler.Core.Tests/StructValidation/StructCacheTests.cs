using Shouldly;
using X2ModCompiler.Core.StructValidation;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Core.Tests.StructValidation;

public class StructCacheTests : TestBase
{
    [Fact]
    public async Task StructCache_Constructor_CreatesCacheDirectory()
    {
        await using var temp = new TempDirectory();
        string newCacheDir = Path.Combine(temp.Path, "new_cache");
        var cache = new StructCache(newCacheDir);
        Directory.Exists(newCacheDir).ShouldBeTrue();
    }

    [Fact]
    public async Task StructCache_TryGet_NonExistentCache_ReturnsFalse()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
        var result = cache.TryGet("NonExistentStruct", out var cached);
        result.ShouldBeFalse();
        cached.ShouldBeNull();
    }

    [Fact]
    public async Task StructCache_Save_ThenTryGet_ReturnsCachedStruct()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
        var tempFile = Path.Combine(temp.Path, "test.uc");
        await File.WriteAllTextAsync(tempFile, "test content", TestContext.Current.CancellationToken);
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

        var cacheFile = Path.Combine(temp.Path, "structsmap", "TestStruct.mempack");
        File.Exists(cacheFile).ShouldBeTrue();

        var result = cache.TryGet("TestStruct", out var cached);
        result.ShouldBeTrue("cache should be valid");
        cached.ShouldNotBeNull();
        cached!.StructName.ShouldBe("TestStruct");
        cached.SourceFile.ShouldBe(tempFile);
    }

    [Fact]
    public async Task StructCache_TryGet_ExpiredCache_ReturnsFalse()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
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
    public async Task StructCache_TryGet_SourceFileDeleted_ReturnsFalse()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
        var tempFile = Path.Combine(temp.Path, "temp.uc");
        await File.WriteAllTextAsync(tempFile, "test content", TestContext.Current.CancellationToken);

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
    public async Task StructCache_TryGet_SourceFileModified_ReturnsFalse()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
        var tempFile = Path.Combine(temp.Path, "temp.uc");
        await File.WriteAllTextAsync(tempFile, "original content", TestContext.Current.CancellationToken);

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
        await File.WriteAllTextAsync(tempFile, "modified content", TestContext.Current.CancellationToken);
        var result = cache.TryGet("ModifiedStruct", out var cached);
        result.ShouldBeFalse();
        cached.ShouldBeNull();
    }

    [Fact]
    public async Task StructCache_TryGet_ValidCache_ReturnsTrue()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
        var tempFile = Path.Combine(temp.Path, "valid.uc");
        await File.WriteAllTextAsync(tempFile, "content", TestContext.Current.CancellationToken);

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
    public async Task StructCache_Save_ThenTryGet_ReturnsCorrectCacheFile()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
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

        var expectedPath = Path.Combine(temp.Path, "structsmap", "TestStruct.mempack");
        File.Exists(expectedPath).ShouldBeTrue();
    }

    [Fact]
    public async Task StructCache_NegativeCache_SaveAndCheck()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);

        cache.SaveNotFound("MissingStruct");

        cache.IsKnownNotFound("MissingStruct").ShouldBeTrue();
        cache.IsKnownNotFound("OtherStruct").ShouldBeFalse();
    }

    [Fact]
    public async Task StructCache_ClearNegativeEntries_RemovesNegativeCache()
    {
        await using var temp = new TempDirectory();
        var cache = new StructCache(temp.Path);
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
