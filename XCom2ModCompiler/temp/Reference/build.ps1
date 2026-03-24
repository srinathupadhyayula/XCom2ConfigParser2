Param(
    [string] $srcDirectory, # the path that contains your mod's .XCOM_sln
    [string] $sdkPath, # the path to your SDK installation ending in "XCOM 2 War of the Chosen SDK"
    [string] $gamePath, # the path to your XCOM 2 installation ending in "XCOM2-WaroftheChosen"
    [string] $config, # build configuration
	[string] $modDestinationPath # the path to the mod's destination directory
)

$ScriptDirectory = Split-Path $MyInvocation.MyCommand.Path
$common = Join-Path -Path $ScriptDirectory "X2ModBuildCommon\build_common.ps1"
Write-Host "Sourcing $common"
. ($common)

$builder = [BuildProject]::new("AdventCoalition", $srcDirectory, $sdkPath, $gamePath, $modDestinationPath)

# Building against Highlander option 1:
# Use Git to add Highlander submodule by running this command in the terminal:
# git submodule add https://github.com/X2CommunityCore/X2WOTCCommunityHighlander.git
# Uncomment the next line to enable building against Highlander.
# $builder.IncludeSrc("$srcDirectory\X2WOTCCommunityHighlander\X2WOTCCommunityHighlander\Src")

# Building against Highlander (custom path for this project):
# $projectRoot = Split-Path $ScriptDirectory -Parent
# $highlanderPath = Join-Path -Path $projectRoot "X2WOTCCommunityHighlander\X2WOTCCommunityHighlander\Src"
# $builder.IncludeSrc($highlanderPath)

# Building against Highlander option 2:
# Specify path to your local Highlander repository or the Highlander's mod folder,
# and uncomment the line:
$builder.IncludeSrc("E:\Games\Steam\steamapps\workshop\content\268500\1134256495\Src")
# AlienHunter Community Highlander
$builder.IncludeSrc("E:\Games\Steam\steamapps\workshop\content\268500\2534737016\Src")


# XModBaseRedux
$builder.IncludeSrc("E:\Games\Steam\steamapps\workshop\content\268500\3137332236\Src")

# BallisticShields
$builder.IncludeSrc("E:\Games\Steam\steamapps\workshop\content\268500\1416242202\Src")

# TruePrimarySecondaries
$builder.IncludeSrc("E:\Games\Steam\steamapps\workshop\content\268500\2133399183\Src")

switch ($config)
{
    "debug" {
        $builder.EnableDebug()
    }
    "default" {
        # Nothing special
    }
    "" { ThrowFailure "Missing build configuration" }
    default { ThrowFailure "Unknown build configuration $config" }
}

# Uncomment this line to enable cooking.
# $builder.SetContentOptionsJsonFilename("ContentOptions.json")

if ([string]::IsNullOrEmpty($modDestinationPath)) {
    $modDestinationPath = "C:\Program Files (x86)\Steam\steamapps\common\XCOM 2\XCom2-WarOfTheChosen\XComGame\Mods"
    Write-Host "Using default mod destination path: $modDestinationPath"
}

$builder.InvokeBuild()