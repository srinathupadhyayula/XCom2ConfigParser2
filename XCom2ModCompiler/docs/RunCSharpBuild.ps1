$ErrorActionPreference = "Continue"

Write-Host "=== C# BUILD STARTING ===" -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()

& 'D:\Projects\Active\Mods\XCOM2_WOTC\Xcom2Modding\AdventCoalition\.scripts\XCom2ModCompiler.exe' build `
    --mod-name "AdventCoalition" `
    --src-directory "D:\Projects\Active\Mods\XCOM2_WOTC\Xcom2Modding\AdventCoalition" `
    --sdk-path "E:\Games\Steam\steamapps\common\XCOM 2 War of the Chosen SDK" `
    --game-path "E:\Games\Steam\steamapps\common\XCOM 2\XCom2-WarOfTheChosen" `
    --mod-destination "D:\Projects\Active\Mods\XCOM2_WOTC\LocalMods" `
    --config "default" `
    --include-src "E:\Games\Steam\steamapps\workshop\content\268500\1134256495\Src" `
    --include-src "E:\Games\Steam\steamapps\workshop\content\268500\2534737016\Src" `
    --include-src "E:\Games\Steam\steamapps\workshop\content\268500\3137332236\Src" `
    --include-src "E:\Games\Steam\steamapps\workshop\content\268500\1416242202\Src" `
    --include-src "E:\Games\Steam\steamapps\workshop\content\268500\2133399183\Src" `
    2>&1 | Out-File -FilePath "D:\Projects\Active\Mods\XCOM2_WOTC\Xcom2Modding\XCom2ConfigParser2\XCom2ModCompiler\docs\CSharpBuild_Direct.log" -Encoding UTF8

$sw.Stop()
Write-Host ""
Write-Host "BUILD COMPLETED IN $($sw.Elapsed.TotalSeconds.toFixed(2))s" -ForegroundColor Cyan

if ($LASTEXITCODE -eq 0) {
    Write-Host "BUILD SUCCESSFUL" -ForegroundColor Green
} else {
    Write-Host "BUILD FAILED WITH EXIT CODE $LASTEXITCODE" -ForegroundColor Red
}
