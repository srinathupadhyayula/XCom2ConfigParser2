Param(
    [string] $srcDirectory,
    [string] $sdkPath,
    [string] $gamePath,
    [string] $config,
    [string] $modDestinationPath,
    [string] $modName,
    [string[]] $includeSrc,
    [string] $contentOptions
)

$ScriptDirectory = Split-Path $MyInvocation.MyCommand.Path

# ============================================================================
# TOGGLE WHICH BUILD SYSTEM TO USE
# ============================================================================

# Use C# version (XCom2ModCompiler.exe)
& "$ScriptDirectory\buildcsharp.ps1" @PSBoundParameters

# Use original PowerShell version (X2ModBuildCommon)
# & "$ScriptDirectory\buildpowershell.ps1" @PSBoundParameters

# ============================================================================
