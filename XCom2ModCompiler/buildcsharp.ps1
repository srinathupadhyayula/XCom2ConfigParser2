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

$ErrorActionPreference = "Stop"

$ScriptDirectory = Split-Path $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path $ScriptDirectory -Parent

# Check for compiler in script directory (deployed directly) or subdirectory
$CompilerExe = Join-Path $ScriptDirectory "XCom2ModCompiler.exe"
if (!(Test-Path $CompilerExe)) {
    # Fallback to subdirectory for development scenarios
    $CompilerExe = Join-Path $ScriptDirectory "XCom2ModCompiler\XCom2ModCompiler.exe"
}

$VsCodeSettingsPath = Join-Path $WorkspaceRoot ".vscode\settings.json"

Write-Host "========================================"
Write-Host "XCom2ModCompiler Build Script"
Write-Host "========================================"

# Check if compiler exists
if (!(Test-Path $CompilerExe)) {
    Write-Host "Error: XCom2ModCompiler.exe not found at $CompilerExe"
    exit 1
}

# Load VS Code settings if available
$vsCodeSettings = $null
if (Test-Path $VsCodeSettingsPath) {
    try {
        $vsCodeSettings = Get-Content $VsCodeSettingsPath | ConvertFrom-Json
        Write-Host "Loaded VS Code settings from $VsCodeSettingsPath"
    } catch {
        Write-Host "Warning: Could not load VS Code settings"
    }
}

# Apply defaults from VS Code settings if parameters not provided
if ([string]::IsNullOrEmpty($sdkPath) -and $vsCodeSettings -and $vsCodeSettings."xcom.highlander.sdkroot") {
    $sdkPath = $vsCodeSettings."xcom.highlander.sdkroot"
    Write-Host "Using SDK path from VS Code settings"
}

if ([string]::IsNullOrEmpty($gamePath) -and $vsCodeSettings -and $vsCodeSettings."xcom.highlander.gameroot") {
    $gamePath = $vsCodeSettings."xcom.highlander.gameroot"
    Write-Host "Using game path from VS Code settings"
}

if ([string]::IsNullOrEmpty($modDestinationPath) -and $vsCodeSettings -and $vsCodeSettings."xcom.highlander.moddestination") {
    $modDestinationPath = $vsCodeSettings."xcom.highlander.moddestination"
    Write-Host "Using mod destination from VS Code settings"
}

# Set default mod destination if still not provided
if ([string]::IsNullOrEmpty($modDestinationPath)) {
    $modDestinationPath = "C:\Program Files (x86)\Steam\steamapps\common\XCOM 2\XCom2-WarOfTheChosen\XComGame\Mods"
    Write-Host "Using default mod destination path"
}

# Extract mod name from srcDirectory if not provided
if ([string]::IsNullOrEmpty($modName)) {
    $modName = Split-Path $WorkspaceRoot -Leaf
    Write-Host "Using mod name from directory: $modName"
}

# Set default srcDirectory if not provided
if ([string]::IsNullOrEmpty($srcDirectory)) {
    $srcDirectory = $WorkspaceRoot
}

# Set default config if not provided
if ([string]::IsNullOrEmpty($config)) {
    $config = "default"
}

Write-Host ""
Write-Host "Build Configuration:"
Write-Host "  Mod Name: $modName"
Write-Host "  Source: $srcDirectory"
Write-Host "  SDK: $sdkPath"
Write-Host "  Game: $gamePath"
Write-Host "  Config: $config"
Write-Host "  Destination: $modDestinationPath"
Write-Host "========================================"
Write-Host ""

# Validate required parameters
if ([string]::IsNullOrEmpty($sdkPath)) {
    Write-Host "Error: Missing SDK path. Provide -sdkPath or set xcom.highlander.sdkroot in .vscode/settings.json" -ForegroundColor Red
    exit 1
}

if ([string]::IsNullOrEmpty($gamePath)) {
    Write-Host "Error: Missing game path. Provide -gamePath or set xcom.highlander.gameroot in .vscode/settings.json" -ForegroundColor Red
    exit 1
}

if ([string]::IsNullOrEmpty($config)) {
    Write-Host "Error: Missing build configuration. Provide -config (default or debug)" -ForegroundColor Red
    exit 1
}

# Build CLI arguments with proper quoting
$cliArgs = "build " +
    "--mod-name `"$modName`" " +
    "--src-directory `"$srcDirectory`" " +
    "--sdk-path `"$sdkPath`" " +
    "--game-path `"$gamePath`" " +
    "--mod-destination `"$modDestinationPath`" " +
    "--config $config"

# Add include paths
if ($includeSrc) {
    foreach ($path in $includeSrc) {
        if (Test-Path $path) {
            $cliArgs += " --include-src `"$path`""
        } else {
            Write-Host "Warning: Include path not found: $path"
        }
    }
}

# Add content options
if ($contentOptions) {
    $cliArgs += " --content-options `"$contentOptions`""
}

Write-Host "Executing: $CompilerExe $cliArgs"
Write-Host ""

# Execute CLI using Start-Process for proper argument handling
$processArgs = @(
    "build"
    "--mod-name", $modName
    "--src-directory", $srcDirectory
    "--sdk-path", $sdkPath
    "--game-path", $gamePath
    "--mod-destination", $modDestinationPath
    "--config", $config
)

if ($includeSrc) {
    foreach ($path in $includeSrc) {
        if (Test-Path $path) {
            $processArgs += "--include-src"
            $processArgs += $path
        }
    }
}

if ($contentOptions) {
    $processArgs += "--content-options"
    $processArgs += $contentOptions
}

$exitCode = 0
try {
    & $CompilerExe @processArgs
    $exitCode = $LASTEXITCODE
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    $exitCode = 1
}

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "BUILD SUCCESSFUL" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "BUILD FAILED" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit $process.ExitCode
}
