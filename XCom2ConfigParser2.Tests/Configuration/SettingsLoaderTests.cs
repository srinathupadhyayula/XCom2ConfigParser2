using Shouldly;
using XCom2ConfigParser2.Configuration;

namespace XCom2ConfigParser2.Tests.Configuration;

public class SettingsLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _vscodeDir;

    public SettingsLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid()}");
        _vscodeDir = Path.Combine(_tempDir, ".vscode");
        Directory.CreateDirectory(_vscodeDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void SettingsLoader_Load_NoSettingsFile_ReturnsDefaults()
    {
        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.IniRoots.Count.ShouldBe(1);
        settings.IniRoots[0].ShouldBe("Config");
        settings.LocalSrcRoot.ShouldBe("Src");
        settings.BuildScriptPath.ShouldBe(".scripts/build.ps1");
        settings.CachePath.ShouldBe(".xcom2cache/structs/");
    }

    [Fact]
    public void SettingsLoader_Load_WithIniRoots_UsesConfiguredValues()
    {
        WriteSettingsJson("""
        {
            "xcom.configParser.iniRoots": ["Config", "XComGame/Config"]
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.IniRoots.Count.ShouldBe(2);
        settings.IniRoots[0].ShouldEndWith("Config");
        settings.IniRoots[1].ShouldEndWith("XComGame" + Path.DirectorySeparatorChar + "Config");
    }

    [Fact]
    public void SettingsLoader_Load_WithLocalSrcRoot_UsesConfiguredValue()
    {
        WriteSettingsJson("""
        {
            "xcom.configParser.localSrcRoot": "MyMod/Src"
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.LocalSrcRoot.ShouldEndWith("MyMod" + Path.DirectorySeparatorChar + "Src");
    }

    [Fact]
    public void SettingsLoader_Load_WithBuildScriptPath_UsesConfiguredValue()
    {
        WriteSettingsJson("""
        {
            "xcom.configParser.buildScriptPath": "scripts/build.ps1"
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.BuildScriptPath.ShouldEndWith("scripts" + Path.DirectorySeparatorChar + "build.ps1");
    }

    [Fact]
    public void SettingsLoader_Load_WithModsCompiledAgainst_UsesConfiguredValues()
    {
        WriteSettingsJson("""
        {
            "xcom.configParser.modsCompiledAgainst": ["../Mod1/Src", "../Mod2/Src"]
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.ModsCompiledAgainst.Count.ShouldBe(2);
        settings.ModsCompiledAgainst[0].ShouldEndWith(".." + Path.DirectorySeparatorChar + "Mod1" + Path.DirectorySeparatorChar + "Src");
        settings.ModsCompiledAgainst[1].ShouldEndWith(".." + Path.DirectorySeparatorChar + "Mod2" + Path.DirectorySeparatorChar + "Src");
    }

    [Fact]
    public void SettingsLoader_Load_WithHighlanderModsRoot_UsesConfiguredValue()
    {
        WriteSettingsJson("""
        {
            "xcom.configParser.communityHighlanderPath": "C:/XCom2/Mods/CommunityHighlander/Src",
            "xcom.configParser.alienHighlanderPath": "C:/XCom2/Mods/AlienHighlander/Src"
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.CommunityHighlanderPath.ShouldContain("CommunityHighlander");
        settings.AlienHighlanderPath.ShouldContain("AlienHighlander");
    }

    [Fact]
    public void SettingsLoader_Load_WithAllModsRoot_UsesConfiguredValue()
    {
        WriteSettingsJson("""
        {
            "xcom.configParser.allModsRoot": "../../AllMods/"
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.AllModsRoot.ShouldContain("AllMods");
    }

    [Fact]
    public void SettingsLoader_Load_WithCachePath_UsesConfiguredValue()
    {
        WriteSettingsJson("""
        {
            "xcom.configParser.cachePath": "cache/structs/"
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.CachePath.ShouldEndWith("cache" + Path.DirectorySeparatorChar + "structs" + Path.DirectorySeparatorChar);
    }

    [Fact]
    public void SettingsLoader_Load_WithSdkRoot_UsesConfiguredValue()
    {
        WriteSettingsJson("""
        {
            "xcom.highlander.sdkroot": "C:/Steam/XCOM2/"
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.SdkRoot.ShouldContain("Steam");
        settings.SdkRoot.ShouldContain("XCOM2");
    }

    [Fact]
    public void SettingsLoader_Load_WithTildePath_ExpandsToHomeDirectory()
    {
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        WriteSettingsJson("""
        {
            "xcom.configParser.communityHighlanderPath": "~/.xcom2/mods/CommunityHighlander/Src"
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.CommunityHighlanderPath.ShouldContain(homeDir);
        settings.CommunityHighlanderPath.ShouldContain("CommunityHighlander");
    }

    [Fact]
    public void SettingsLoader_Load_WithWorkspaceFolderToken_ExpandsToProjectRoot()
    {
        WriteSettingsJson("""
        {
            "xcom.configParser.localSrcRoot": "${workspaceFolder}/Source/Src"
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.LocalSrcRoot.ShouldStartWith(_tempDir);
        settings.LocalSrcRoot.ShouldContain("Source");
        settings.LocalSrcRoot.ShouldContain("Src");
    }

    [Fact]
    public void SettingsLoader_Load_MalformedJson_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(_vscodeDir, "settings.json"), "{ invalid json }");
        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.IniRoots.Count.ShouldBe(1);
        settings.IniRoots[0].ShouldBe("Config");
    }

    [Fact]
    public void SettingsLoader_Load_ParsesBuildScriptForModsCompiledAgainst()
    {
        var buildScriptDir = Path.Combine(_tempDir, ".scripts");
        Directory.CreateDirectory(buildScriptDir);
        string buildScriptPath = Path.Combine(buildScriptDir, "build.ps1");
        File.WriteAllText(buildScriptPath, """
            $builder.IncludeSrc("../CommunityHighlander/Src")
            $builder.IncludeSrc("../AlienHighlander/Src")
            # $builder.IncludeSrc("../CommentedOut/Src")
            """);

        WriteSettingsJson("""
        {
            "xcom.configParser.buildScriptPath": ".scripts/build.ps1"
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.ModsCompiledAgainst.Count.ShouldBe(2);
        settings.ModsCompiledAgainst[0].ShouldContain("CommunityHighlander");
        settings.ModsCompiledAgainst[1].ShouldContain("AlienHighlander");
    }

    [Fact]
    public void SettingsLoader_Load_ExplicitModsCompiledAgainst_TakesPrecedenceOverBuildScript()
    {
        var buildScriptDir = Path.Combine(_tempDir, ".scripts");
        Directory.CreateDirectory(buildScriptDir);
        File.WriteAllText(Path.Combine(buildScriptDir, "build.ps1"), """
            $builder.IncludeSrc("../FromBuildScript/Src")
            """);

        WriteSettingsJson("""
        {
            "xcom.configParser.modsCompiledAgainst": ["../Explicit/Src"]
        }
        """);

        var loader = new SettingsLoader(_tempDir);
        var settings = loader.Load();

        settings.ModsCompiledAgainst.Count.ShouldBe(1);
        settings.ModsCompiledAgainst[0].ShouldContain("Explicit");
    }

    private void WriteSettingsJson(string content)
    {
        File.WriteAllText(Path.Combine(_vscodeDir, "settings.json"), content);
    }
}
