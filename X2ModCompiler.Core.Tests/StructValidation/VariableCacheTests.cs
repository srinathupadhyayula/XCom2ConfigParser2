using Shouldly;
using X2ModCompiler.Core.StructValidation;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Core.Tests.StructValidation;

/// <summary>
/// Tests for <see cref="VariableCache"/> — session-scoped in-memory variable type cache.
/// </summary>
public class VariableCacheTests : TestBase
{
    [Fact]
    public async Task VariableCache_TryGet_MissingEntry_ReturnsFalse()
    {
        await using var temp = new TempDirectory();
        var cache = new VariableCache(temp.Path);

        var found = cache.TryGet("[MyPackage.MyClass]", "MyProp", out var result);

        found.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public async Task VariableCache_Set_ThenTryGet_ReturnsCachedValue()
    {
        await using var temp = new TempDirectory();
        var cache = new VariableCache(temp.Path);
        var expected = VariableTypeResolutionResult.Success("MyStruct", "MyStruct[]", "", Array.Empty<string>());

        cache.Set("[MyPackage.MyClass]", "MyProp", expected);
        
        var cacheFile = Path.Combine(temp.Path, "variablesmap", "[MyPackage.MyClass]_MyProp.mempack");
        File.Exists(cacheFile).ShouldBeTrue();
        
        var found = cache.TryGet("[MyPackage.MyClass]", "MyProp", out var result);

        found.ShouldBeTrue();
        result.ShouldNotBeNull();
        result!.BaseType.ShouldBe("MyStruct");
        result.FullType.ShouldBe("MyStruct[]");
    }

    [Fact]
    public async Task VariableCache_IsCaseInsensitive_OnPropertyName()
    {
        await using var temp = new TempDirectory();
        var cache = new VariableCache(temp.Path);
        var value = VariableTypeResolutionResult.Success("int", "int", "", Array.Empty<string>());

        cache.Set("[Pkg.Class]", "MyProperty", value);

        cache.TryGet("[Pkg.Class]", "myproperty", out _).ShouldBeTrue();
        cache.TryGet("[Pkg.Class]", "MYPROPERTY", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task VariableCache_IsCaseInsensitive_OnSectionName()
    {
        await using var temp = new TempDirectory();
        var cache = new VariableCache(temp.Path);
        var value = VariableTypeResolutionResult.Success("int", "int", "", Array.Empty<string>());

        cache.Set("[MyPkg.MyClass]", "Prop", value);

        cache.TryGet("[mypkg.myclass]", "Prop", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task VariableCache_DifferentProperties_AreCachedIndependently()
    {
        await using var temp = new TempDirectory();
        var cache = new VariableCache(temp.Path);
        var v1 = VariableTypeResolutionResult.Success("int", "int", "", Array.Empty<string>());
        var v2 = VariableTypeResolutionResult.Success("bool", "bool", "", Array.Empty<string>());

        cache.Set("[Pkg.Class]", "Prop1", v1);
        cache.Set("[Pkg.Class]", "Prop2", v2);

        cache.TryGet("[Pkg.Class]", "Prop1", out var r1).ShouldBeTrue();
        cache.TryGet("[Pkg.Class]", "Prop2", out var r2).ShouldBeTrue();
        r1!.BaseType.ShouldBe("int");
        r2!.BaseType.ShouldBe("bool");
    }

    [Fact]
    public async Task VariableCache_DifferentSections_AreCachedIndependently()
    {
        await using var temp = new TempDirectory();
        var cache = new VariableCache(temp.Path);
        var v1 = VariableTypeResolutionResult.Success("int", "int", "", Array.Empty<string>());
        var v2 = VariableTypeResolutionResult.Success("bool", "bool", "", Array.Empty<string>());

        cache.Set("[PkgA.ClassA]", "Prop", v1);
        cache.Set("[PkgB.ClassB]", "Prop", v2);

        cache.TryGet("[PkgA.ClassA]", "Prop", out var r1).ShouldBeTrue();
        cache.TryGet("[PkgB.ClassB]", "Prop", out var r2).ShouldBeTrue();
        r1!.BaseType.ShouldBe("int");
        r2!.BaseType.ShouldBe("bool");
    }

    [Fact]
    public async Task VariableCache_NotFound_CanAlsoBeStored()
    {
        await using var temp = new TempDirectory();
        var cache = new VariableCache(temp.Path);
        var notFound = VariableTypeResolutionResult.NotFound(new[] { "Searched/Path.uc" });

        cache.Set("[Pkg.Class]", "Missing", notFound);
        var isKnown = cache.IsKnownNotFound("[Pkg.Class]", "Missing");

        isKnown.ShouldBeTrue("entry was set even if resolution was not found");
    }
}
