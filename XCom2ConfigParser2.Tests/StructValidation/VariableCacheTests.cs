using Shouldly;
using XCom2ConfigParser2.Configuration;
using XCom2ConfigParser2.StructValidation;

namespace XCom2ConfigParser2.Tests.StructValidation;

/// <summary>
/// Tests for <see cref="VariableCache"/> — session-scoped in-memory variable type cache.
/// </summary>
public class VariableCacheTests
{
    [Fact]
    public void VariableCache_TryGet_MissingEntry_ReturnsFalse()
    {
        var cache = new VariableCache();

        var found = cache.TryGet("[MyPackage.MyClass]", "MyProp", out var result);

        found.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void VariableCache_Set_ThenTryGet_ReturnsCachedValue()
    {
        var cache = new VariableCache();
        var expected = VariableTypeResolutionResult.Success("MyStruct", "MyStruct[]", "Src/Test.uc", Array.Empty<string>());

        cache.Set("[MyPackage.MyClass]", "MyProp", expected);
        var found = cache.TryGet("[MyPackage.MyClass]", "MyProp", out var result);

        found.ShouldBeTrue();
        result.ShouldNotBeNull();
        result!.BaseType.ShouldBe("MyStruct");
        result.FullType.ShouldBe("MyStruct[]");
    }

    [Fact]
    public void VariableCache_IsCaseInsensitive_OnPropertyName()
    {
        var cache = new VariableCache();
        var value = VariableTypeResolutionResult.Success("int", "int", "Src/Test.uc", Array.Empty<string>());

        cache.Set("[Pkg.Class]", "MyProperty", value);

        cache.TryGet("[Pkg.Class]", "myproperty", out _).ShouldBeTrue();
        cache.TryGet("[Pkg.Class]", "MYPROPERTY", out _).ShouldBeTrue();
    }

    [Fact]
    public void VariableCache_IsCaseInsensitive_OnSectionName()
    {
        var cache = new VariableCache();
        var value = VariableTypeResolutionResult.Success("int", "int", "Src/Test.uc", Array.Empty<string>());

        cache.Set("[MyPkg.MyClass]", "Prop", value);

        cache.TryGet("[mypkg.myclass]", "Prop", out _).ShouldBeTrue();
    }

    [Fact]
    public void VariableCache_DifferentProperties_AreCachedIndependently()
    {
        var cache = new VariableCache();
        var v1 = VariableTypeResolutionResult.Success("int", "int", "f.uc", Array.Empty<string>());
        var v2 = VariableTypeResolutionResult.Success("bool", "bool", "f.uc", Array.Empty<string>());

        cache.Set("[Pkg.Class]", "Prop1", v1);
        cache.Set("[Pkg.Class]", "Prop2", v2);

        cache.TryGet("[Pkg.Class]", "Prop1", out var r1).ShouldBeTrue();
        cache.TryGet("[Pkg.Class]", "Prop2", out var r2).ShouldBeTrue();
        r1!.BaseType.ShouldBe("int");
        r2!.BaseType.ShouldBe("bool");
    }

    [Fact]
    public void VariableCache_DifferentSections_AreCachedIndependently()
    {
        var cache = new VariableCache();
        var v1 = VariableTypeResolutionResult.Success("int", "int", "f.uc", Array.Empty<string>());
        var v2 = VariableTypeResolutionResult.Success("bool", "bool", "f.uc", Array.Empty<string>());

        cache.Set("[PkgA.ClassA]", "Prop", v1);
        cache.Set("[PkgB.ClassB]", "Prop", v2);

        cache.TryGet("[PkgA.ClassA]", "Prop", out var r1).ShouldBeTrue();
        cache.TryGet("[PkgB.ClassB]", "Prop", out var r2).ShouldBeTrue();
        r1!.BaseType.ShouldBe("int");
        r2!.BaseType.ShouldBe("bool");
    }

    [Fact]
    public void VariableCache_NotFound_CanAlsoBeStored()
    {
        var cache = new VariableCache();
        var notFound = VariableTypeResolutionResult.NotFound(new[] { "Searched/Path.uc" });

        cache.Set("[Pkg.Class]", "Missing", notFound);
        var found = cache.TryGet("[Pkg.Class]", "Missing", out var result);

        found.ShouldBeTrue("entry was set even if resolution was not found");
        result!.Found.ShouldBeFalse();
    }
}
