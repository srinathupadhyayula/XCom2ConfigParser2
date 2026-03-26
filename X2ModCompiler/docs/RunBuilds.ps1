param(
    [string]$BuildType = "Both"  # CSharp, PowerShell, or Both
)

$ErrorActionPreference = "Continue"
$LogPath = Split-Path $MyInvocation.MyCommand.Path
$BuildScriptPath = "D:\Projects\Active\Mods\XCOM2_WOTC\Xcom2Modding\AdventCoalition\.scripts\build.ps1"

$commonArgs = @{
    srcDirectory = "D:\Projects\Active\Mods\XCOM2_WOTC\Xcom2Modding\AdventCoalition"
    sdkPath = "E:\Games\Steam\steamapps\common\XCOM 2 War of the Chosen SDK"
    gamePath = "E:\Games\Steam\steamapps\common\XCOM 2\XCom2-WarOfTheChosen"
    config = "default"
    modDestinationPath = "D:\Projects\Active\Mods\XCOM2_WOTC\LocalMods"
    modName = "AdventCoalition"
}

function Run-Build {
    param(
        [string]$Name,
        [string]$LogFile,
        [scriptblock]$SetupAction
    )
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  STARTING $Name BUILD" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    # Run setup action if provided (e.g., toggle build system)
    if ($SetupAction) {
        & $SetupAction
    }
    
    # Clean previous log
    if (Test-Path $LogFile) {
        Remove-Item $LogFile -Force
    }
    
    # Start timer
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    
    # Run build and capture output
    $output = & $BuildScriptPath @commonArgs 2>&1 | Out-String -Stream
    $output | Out-File -FilePath $LogFile -Encoding UTF8
    $output | ForEach-Object { Write-Host $_ }
    
    $sw.Stop()
    
    # Write summary
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  $Name BUILD COMPLETED IN $($sw.Elapsed.TotalSeconds.ToString("F2"))s" -ForegroundColor Cyan
    Write-Host "  Log: $LogFile" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    return $sw.Elapsed.TotalSeconds
}

# Run C# Build
if ($BuildType -eq "Both" -or $BuildType -eq "CSharp") {
    $csharpSetup = {
        $buildPsPath = "D:\Projects\Active\Mods\XCOM2_WOTC\Xcom2Modding\AdventCoalition\.scripts\build.ps1"
        $content = Get-Content $buildPsPath -Raw
        $content = $content -replace '^#?\s*&\s*"\$ScriptDirectory\\buildcsharp\.ps1"', '& "$ScriptDirectory\buildcsharp.ps1"'
        $content = $content -replace '^#?\s*&\s*"\$ScriptDirectory\\buildpowershell\.ps1"', '# & "$ScriptDirectory\buildpowershell.ps1"'
        Set-Content $buildPsPath -Value $content -NoNewline
        Write-Host "Configured for C# build"
    }
    
    $csharpTime = Run-Build -Name "C#" -LogFile "$LogPath\CSharpBuild.log" -SetupAction $csharpSetup
    
    if ($BuildType -eq "CSharp") { return }
}

# Run PowerShell Build
if ($BuildType -eq "Both" -or $BuildType -eq "PowerShell") {
    $psSetup = {
        $buildPsPath = "D:\Projects\Active\Mods\XCOM2_WOTC\Xcom2Modding\AdventCoalition\.scripts\build.ps1"
        $content = Get-Content $buildPsPath -Raw
        $content = $content -replace '^#?\s*&\s*"\$ScriptDirectory\\buildcsharp\.ps1"', '# & "$ScriptDirectory\buildcsharp.ps1"'
        $content = $content -replace '^#?\s*&\s*"\$ScriptDirectory\\buildpowershell\.ps1"', '& "$ScriptDirectory\buildpowershell.ps1"'
        Set-Content $buildPsPath -Value $content -NoNewline
        Write-Host "Configured for PowerShell build"
    }
    
    $psTime = Run-Build -Name "PowerShell" -LogFile "$LogPath\PowerShellBuild.log" -SetupAction $psSetup
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ALL BUILDS COMPLETED" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Logs available at:"
Write-Host "  C#:       $LogPath\CSharpBuild.log"
Write-Host "  PowerShell: $LogPath\PowerShellBuild.log"
Write-Host ""
