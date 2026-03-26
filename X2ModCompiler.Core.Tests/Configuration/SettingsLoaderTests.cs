using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using X2ModCompiler.Core.Configuration;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Core.Tests.Configuration;

public class SettingsLoaderTests : TestBase
{
    private readonly ILogger<SettingsLoader> _logger;

    public SettingsLoaderTests()
    {
        _logger = Substitute.For<ILogger<SettingsLoader>>();
    }

    [Fact]
    public async Task SettingsLoader_Load_NoSettingsFile_ReturnsDefaults()
    {
        await using var temp = new TempDirectory();
        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.IniRoots.Count.ShouldBe(1);
        settings.IniRoots[0].ShouldEndWith("Config");
        settings.LocalSrcRoot.ShouldEndWith("Src");
        settings.BuildScriptPath.ShouldEndWith(Path.Combine(".scripts", "build.ps1"));
        settings.CachePath.ShouldEndWith(".xcom2cache");
    }

    [Fact]
    public async Task SettingsLoader_Load_WithIniRoots_UsesConfiguredValues()
    {
        await using var temp = new TempDirectory();
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.iniRoots": ["Config", "XComGame/Config"]
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.IniRoots.Count.ShouldBe(2);
        settings.IniRoots[0].ShouldEndWith("Config");
        settings.IniRoots[1].ShouldEndWith("XComGame" + Path.DirectorySeparatorChar + "Config");
    }

    [Fact]
    public async Task SettingsLoader_Load_WithLocalSrcRoot_UsesConfiguredValue()
    {
        await using var temp = new TempDirectory();
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.localSrcRoot": "MyMod/Src"
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.LocalSrcRoot.ShouldEndWith("MyMod" + Path.DirectorySeparatorChar + "Src");
    }

    [Fact]
    public async Task SettingsLoader_Load_WithBuildScriptPath_UsesConfiguredValue()
    {
        await using var temp = new TempDirectory();
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.buildScriptPath": "scripts/build.ps1"
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.BuildScriptPath.ShouldEndWith("scripts" + Path.DirectorySeparatorChar + "build.ps1");
    }

    [Fact]
    public async Task SettingsLoader_Load_WithModsCompiledAgainst_UsesConfiguredValues()
    {
        await using var temp = new TempDirectory();
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.modsCompiledAgainst": ["../Mod1/Src", "../Mod2/Src"]
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.ModsCompiledAgainst.Count.ShouldBe(2);
        // Check that paths contain the expected mod names (actual path format may vary)
        settings.ModsCompiledAgainst[0].ShouldContain("Mod1");
        settings.ModsCompiledAgainst[1].ShouldContain("Mod2");
    }

    [Fact]
    public async Task SettingsLoader_Load_WithHighlanderModsRoot_UsesConfiguredValue()
    {
        await using var temp = new TempDirectory();
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.communityHighlanderPath": "C:/XCom2/Mods/CommunityHighlander/Src",
            "X2ModCompiler.alienHighlanderPath": "C:/XCom2/Mods/AlienHighlander/Src"
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.CommunityHighlanderPath!.ShouldContain("CommunityHighlander");
        settings.AlienHighlanderPath!.ShouldContain("AlienHighlander");
    }

    [Fact]
    public async Task SettingsLoader_Load_WithAllModsRoot_UsesConfiguredValue()
    {
        await using var temp = new TempDirectory();
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.allModsRoot": "../../AllMods/"
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.AllModsRoot.ShouldContain("AllMods");
    }

    [Fact]
    public async Task SettingsLoader_Load_WithCachePath_UsesConfiguredValue()
    {
        await using var temp = new TempDirectory();
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.cachePath": "cache/structs/"
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.CachePath.ShouldContain(Path.Combine("cache", "structs"));
    }

    [Fact]
    public async Task SettingsLoader_Load_WithSdkRoot_UsesConfiguredValue()
    {
        await using var temp = new TempDirectory();
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.sdkRoot": "C:/Steam/XCOM2/"
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.SdkRoot!.ShouldContain("Steam");
        settings.SdkRoot!.ShouldContain("XCOM2");
    }

    [Fact]
    public async Task SettingsLoader_Load_WithTildePath_ExpandsToHomeDirectory()
    {
        await using var temp = new TempDirectory();
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.communityHighlanderPath": "~/.xcom2/mods/CommunityHighlander/Src"
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.CommunityHighlanderPath!.ShouldContain(homeDir);
        settings.CommunityHighlanderPath!.ShouldContain("CommunityHighlander");
    }

    [Fact]
    public async Task SettingsLoader_Load_WithWorkspaceFolderToken_ExpandsToProjectRoot()
    {
        await using var temp = new TempDirectory();
        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.localSrcRoot": "${workspaceFolder}/Source/Src"
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.LocalSrcRoot.ShouldStartWith(temp.Path);
        settings.LocalSrcRoot.ShouldContain("Source");
        settings.LocalSrcRoot.ShouldContain("Src");
    }

    [Fact]
    public async Task SettingsLoader_Load_MalformedJson_ReturnsDefaults()
    {
        await using var temp = new TempDirectory();
        var vscodeDir = Path.Combine(temp.Path, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        await File.WriteAllTextAsync(Path.Combine(vscodeDir, "settings.json"), "{ invalid json }", TestContext.Current.CancellationToken);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.IniRoots.Count.ShouldBe(1);
        settings.IniRoots[0].ShouldEndWith("Config");
    }

    [Fact]
    public async Task SettingsLoader_Load_ParsesBuildScriptForModsCompiledAgainst()
    {
        await using var temp = new TempDirectory();
        var buildScriptDir = Path.Combine(temp.Path, ".scripts");
        Directory.CreateDirectory(buildScriptDir);
        string buildScriptPath = Path.Combine(buildScriptDir, "build.ps1");
        await File.WriteAllTextAsync(buildScriptPath, """
            $builder.IncludeSrc("../CommunityHighlander/Src")
            $builder.IncludeSrc("../AlienHighlander/Src")
            # $builder.IncludeSrc("../CommentedOut/Src")
            """, TestContext.Current.CancellationToken);

        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.buildScriptPath": ".scripts/build.ps1"
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.ModsCompiledAgainst.Count.ShouldBe(2);
        settings.ModsCompiledAgainst[0].ShouldContain("CommunityHighlander");
        settings.ModsCompiledAgainst[1].ShouldContain("AlienHighlander");
    }

    [Fact]
    public async Task SettingsLoader_Load_ExplicitModsCompiledAgainst_TakesPrecedenceOverBuildScript()
    {
        await using var temp = new TempDirectory();
        var buildScriptDir = Path.Combine(temp.Path, ".scripts");
        Directory.CreateDirectory(buildScriptDir);
        await File.WriteAllTextAsync(Path.Combine(buildScriptDir, "build.ps1"), """
            $builder.IncludeSrc("../FromBuildScript/Src")
            """, TestContext.Current.CancellationToken);

        WriteSettingsJson(temp.Path, """
        {
            "X2ModCompiler.modsCompiledAgainst": ["../Explicit/Src"]
        }
        """);

        var loader = new SettingsLoader(temp.Path, _logger);
        var settings = loader.Load();

        settings.ModsCompiledAgainst.Count.ShouldBe(1);
        settings.ModsCompiledAgainst[0].ShouldContain("Explicit");
    }

    private void WriteSettingsJson(string projectRoot, string content)
    {
        var vscodeDir = Path.Combine(projectRoot, ".vscode");
        Directory.CreateDirectory(vscodeDir);
        File.WriteAllText(Path.Combine(vscodeDir, "settings.json"), content);
    }
}
