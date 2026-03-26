using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using X2ModCompiler.Core.Configuration;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Core.Tests.Validation;

public class ModSrcPathCacheTests : TestBase
{
    private readonly ILogger<ModSrcPathCache> _logger;

    public ModSrcPathCacheTests()
    {
        _logger = Substitute.For<ILogger<ModSrcPathCache>>();
    }

    [Fact]
    public async Task ModSrcPathCache_DiscoversRoots_WithSrcSubdirectory()
    {
        await using var temp = new TempDirectory();
        var modsRoot = Path.Combine(temp.Path, "Mods");
        var mod1 = Path.Combine(modsRoot, "Mod1");
        var mod2 = Path.Combine(modsRoot, "Mod2");
        Directory.CreateDirectory(Path.Combine(mod1, "Src"));
        Directory.CreateDirectory(Path.Combine(mod2, "Src"));
        Directory.CreateDirectory(Path.Combine(modsRoot, "NotAMod")); // No Src

        var settings = new ParserSettings { AllModsRoot = modsRoot };
        var cache = new ModSrcPathCache(settings, _logger);

        cache.AllModSrcRoots.Count.ShouldBe(2);
        cache.AllModSrcRoots.ShouldContain(Path.Combine(mod1, "Src"));
        cache.AllModSrcRoots.ShouldContain(Path.Combine(mod2, "Src"));
    }

    [Fact]
    public async Task ModSrcPathCache_ExcludesExplicitlyConfiguredPaths()
    {
        await using var temp = new TempDirectory();
        var modsRoot = Path.Combine(temp.Path, "Mods");
        var mod1 = Path.Combine(modsRoot, "Mod1");
        var mod2 = Path.Combine(modsRoot, "Mod2");
        var mod1Src = Path.Combine(mod1, "Src");
        var mod2Src = Path.Combine(mod2, "Src");
        Directory.CreateDirectory(mod1Src);
        Directory.CreateDirectory(mod2Src);

        var settings = new ParserSettings 
        { 
            AllModsRoot = modsRoot,
            ModsCompiledAgainst = new List<string> { mod1Src }
        };
        
        var cache = new ModSrcPathCache(settings, _logger);

        cache.AllModSrcRoots.Count.ShouldBe(1);
        cache.AllModSrcRoots[0].ShouldBe(mod2Src);
    }

    [Fact]
    public async Task ModSrcPathCache_HandlesMissingRootGracefully()
    {
        await using var temp = new TempDirectory();
        var settings = new ParserSettings { AllModsRoot = Path.Combine(temp.Path, "NonExistent") };
        var cache = new ModSrcPathCache(settings, _logger);

        cache.AllModSrcRoots.ShouldBeEmpty();
    }
}
