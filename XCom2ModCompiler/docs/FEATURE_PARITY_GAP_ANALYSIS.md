# XCom2ModCompiler vs PowerShell Build Common - Comprehensive Parity Analysis

**Date:** 2026-03-23  
**Analysis Type:** Full Build Step Comparison  
**Reference:** build_common.ps1 (1754 lines, 59,964 bytes) vs BuildController.cs (890 lines)

---

## Executive Summary

**CRITICAL FINDING:** The C# implementation is **NOT at parity** with the PowerShell version. Multiple critical build steps are missing or implemented incorrectly, resulting in:

1. **AdventCoalition.u NOT being compiled** - C# reports "Can't find files" while PowerShell compiles it successfully
2. **ItemGroup regeneration completely missing** - 600+ lines of detailed logging absent in C#
3. **Mod source files NOT copied to correct location** - Root cause of compilation failure
4. **Build time 5 seconds slower** (35.67s vs 30.70s)

---

## Complete Build Step Comparison

### PowerShell Build Steps (In Order)

| # | Step | Method | Detailed Logging | Output |
|---|------|--------|-----------------|--------|
| 1 | _ConfirmPaths() | Validate SDK and Game paths | ✅ SDK Path, Game Path | Console output |
| 2 | _SetupUtils() | Initialize build utilities | ✅ Package lists, paths | Internal setup |
| 3 | _LoadContentOptions() | Load ContentOptions.json | ✅ "No missing uncooked", "No packages to make SF", etc. | Content options |
| 4 | **_RegenerateItemGroup()** | **Regenerate .x2proj ItemGroup** | ✅ **~600 lines** - "Removed Content...", "Added Content...", "Found X files and Y folders" | **Updated .x2proj** |
| 5 | _CleanAdditional() | Clean additional mods | ✅ "Cleaning {mod}..." | Cleaned directories |
| 6 | _CopyModToSdk() | Mirror mod to staging | ✅ "Copying mod project to staging...", "Copied project to staging", "Reading mod metadata...", "Read.", "Writing mod metadata...", "Written." | Staging directory |
| 7 | _ConvertLocalization() | Convert UTF-8 to UTF-16 | ✅ "Converting Localization UTF-8 -> UTF-16..." | UTF-16 files |
| 8 | _CopyToSrc() | Copy sources to Dev Src | ✅ "Mirroring SrcOrig to Src...", "Copying dependency sources to Src...", **lists each dependency**, "Copying the mod's sources to Src..." | Development\Src populated |
| 9 | _RunPreMakeHooks() | Execute pre-make hooks | ✅ "Running Pre-Make hooks..." | Hooks executed |
| 10 | _CheckCleanCompiled() | Selective clean check | ✅ "Detected change in macros", "Selective cleaning...", "Cleaned." | Cleaned script packages |
| 11 | _RunMakeBase() | Compile base packages | ✅ Make commandlet output with colors | Compiled base packages |
| 12 | _RunMakeMod() | Compile mod packages | ✅ Make commandlet output with colors, **"Scripts successfully compiled - saving package '...AdventCoalition.u'"** | **AdventCoalition.u compiled** |
| 13 | _RecordCoreTimestamp() | Record Core.u timestamp | ✅ (silent) | Fingerprint saved |
| 14 | _CopyScriptPackages() | Copy to staging | ✅ Lists each package path | Packages in staging |
| 15 | _CleanLeftoverScripts() | Clean SDK/Game scripts | ✅ "Cleaning leftover script {path}" | Cleaned scripts |
| 16 | _PrecompileShaders() | Shader precompilation | ✅ "Checking the need to PrecompileShaders", "No reason to precompile shaders, using existing" | Shader cache |
| 17 | _RunCookAssets() | Cook mod assets | ✅ "No asset cooking is requested, skipping" | Assets cooked/skipped |
| 18 | _CopyMissingUncooked() | Copy missing uncooked | ✅ "Including MissingUncooked" or "Skipping Missing Uncooked logic" | Uncooked packages |
| 19 | _FinalCopy() | Copy to game directory | ✅ "Copying built mod to game directory...", "Removing source staging directory...", "Source directory removed. Move operation completed successfully." | Mod in game directory |

**Total Build Time:** 30.70 seconds  
**Total Log Lines:** 885

---

### C# Build Steps (In Order)

| # | Step | Method | Detailed Logging | Output |
|---|------|--------|-----------------|--------|
| 1 | ValidateConfiguration() | Validate paths | ❌ Only ILogger (minimal) | Console output |
| 2 | **_projectSynchronizer.Synchronize()** | **Sync .x2proj** | ❌ **NO ItemGroup regeneration** - silent or minimal | **NO .x2proj update** |
| 3 | Clean additional mods | Clean mods | ✅ ILogger | Cleaned directories |
| 4 | Mirror mod to staging | Copy to staging | ✅ ILogger | Staging directory |
| 5 | Copy Src to SDK | Copy Src folder | ❌ **COPIES TO WRONG LOCATION** - `SDK\Src\ModName` instead of `SDK\Src\` | **Files in wrong place** |
| 6 | Generate XComMod file | Create metadata | ✅ ILogger | .XComMod file |
| 7 | Convert localization | UTF-8 to UTF-16 | ✅ ILogger | UTF-16 files |
| 8 | Load content options | Load JSON | ✅ ILogger (partial) | Content options |
| 9 | CheckCleanCompiled | Selective clean | ✅ ILogger | Cleaned packages |
| 10 | Precompile shaders | Shader cache | ✅ ILogger | Shader cache |
| 11 | Run pre-make hooks | Execute hooks | ✅ ILogger | Hooks executed |
| 12 | **Copy dependency sources** | **Copy IncludeSrc** | ⚠️ **Console.WriteLine (bypasses logger)** | Development\Src |
| 13 | Compile base packages | Make commandlet | ✅ MakeOutputReceiver | Compiled base |
| 14 | Compile mod packages | Make commandlet | ✅ MakeOutputReceiver | **FAILS - "Can't find files"** |
| 15 | Record Core timestamp | Fingerprint | ✅ ILogger | Fingerprint saved |
| 16 | Copy script packages | Copy to staging | ✅ ILogger + individual package logging | Packages in staging |
| 17 | Clean leftover scripts | Clean scripts | ✅ ILogger | Cleaned scripts |
| 18 | Cook assets | Asset cooking | ✅ AssetCooker | Assets cooked/skipped |
| 19 | Copy missing uncooked | Copy packages | ✅ ILogger | Uncooked packages |
| 20 | Final copy | Mirror to game | ✅ ILogger | Mod in game directory |

**Total Build Time:** 35.67 seconds  
**Total Log Lines:** ~200

---

## Critical Differences Identified

### 1. ItemGroup Regeneration - COMPLETELY MISSING

**PowerShell:**
- Scans project directory for ALL files and folders
- Removes existing ItemGroups from .x2proj
- Logs EVERY file: "Removed Content 'X' from ItemGroups", "Added Content 'X' to ItemGroups"
- Found 278 files and 82 folders (for AdventCoalition)
- **~600 lines of detailed logging**

**C#:**
- ProjectSynchronizer exists but does NOT regenerate ItemGroup
- No file/folder scanning
- No detailed logging
- **0 lines of ItemGroup logging**

**Impact:** .x2proj file may be out of sync with actual project contents.

---

### 2. Mod Source Copy Location - WRONG PATH

**PowerShell _CopyToSrc():**
```powershell
$this._CopySrcFolder("$($this.modSrcRoot)\Src")
# Copies: ModDir\Src\* -> SDK\Development\Src\
```

**C# BuildController.cs (line ~230):**
```csharp
var sdkSrcPath = Path.Combine(_options.SdkPath, "Development", "Src", _options.ModNameCanonical);
var srcFolderPath = Path.Combine(_options.ModSrcRoot, "Src");
await _mirror.MirrorAsync(srcFolderPath, sdkSrcPath, ct: ct);
```
**Copies to:** `SDK\Development\Src\AdventCoalition\` (WRONG!)  
**Should copy to:** `SDK\Development\Src\` (directly, merging contents)

**Impact:** Make commandlet searches `SDK\Development\Src\AdventCoalition\Classes\*.uc` but files are at `SDK\Development\Src\AdventCoalition\AdventCoalition\Classes\X2DLCInfo_AdventCoalition.uc`

**This is the ROOT CAUSE of "Warning, Can't find files matching" error.**

---

### 3. Dependency Source Copy - INCONSISTENT LOGGING

**PowerShell:**
```powershell
Write-Host "Copying dependency sources to Src..."
foreach ($depfolder in $this.include) {
    Get-ChildItem "$($depfolder)" -Directory -Name | Write-Host
    $this._CopySrcFolder($depfolder)
}
Write-Host "Copied dependency sources to Src."
```

**C#:**
```csharp
Console.WriteLine("Copying dependency sources to Src...");  // Bypasses logger!
foreach (var include in _options.IncludePaths) {
    Console.WriteLine(new DirectoryInfo(include).Name);  // Bypasses logger!
    // ... copy logic
}
Console.WriteLine("Copied dependency sources to Src.");  // Bypasses logger!
```

**Impact:** Inconsistent logging - uses Console.WriteLine instead of ILogger, breaking log capture.

---

### 4. Make Commandlet Output - AdventCoalition.u Compilation

**PowerShell:**
```
--------------------AdventCoalition - Release--------------------
Analyzing...
Scripts successfully compiled - saving package 'E:\...\Script\AdventCoalition.u'
```

**C#:**
```
--------------------AdventCoalition - Release--------------------
Warning, Can't find files matching E:\...\Development\Src\AdventCoalition\Classes\*.uc
```

**Impact:** C# version FAILS to compile AdventCoalition.u due to incorrect source file location.

---

### 5. Selective Clean Logging - DIFFERENT DETECTION MESSAGES

**PowerShell:**
```
Detected change in macros (Globals.uci).
Selective cleaning of compiled scripts from E:\...\XComGame\Script to avoid compiler error...
Cleaned.
```

**C#:**
```
Detected change in macros (Globals.uci).
```
(No "Selective cleaning..." or "Cleaned." messages)

**Impact:** Less visibility into selective clean process.

---

### 6. Final Copy - MISSING CLEANUP LOGGING

**PowerShell:**
```
Copying built mod to game directory...
Removing source staging directory E:\...\Mods\AdventCoalition after successful move to D:\...\LocalMods\AdventCoalition...
Source directory removed. Move operation completed successfully.
Copied built mod to game directory in 0.16s
```

**C#:**
```
info: Copied output to final destination in 0.0516217s
```

**Impact:** Less visibility into final copy and cleanup process.

---

## Root Cause Analysis

### Primary Issue: Incorrect Source File Location

The C# implementation copies mod source files to the WRONG location:

**Current (WRONG):**
```
ModDir\Src\AdventCoalition\Classes\X2DLCInfo_AdventCoalition.uc
    ↓ Mirror to
SDK\Development\Src\AdventCoalition\AdventCoalition\Classes\X2DLCInfo_AdventCoalition.uc
```

**PowerShell (CORRECT):**
```
ModDir\Src\AdventCoalition\Classes\X2DLCInfo_AdventCoalition.uc
    ↓ Copy-Item "$includeDir\*" "$devSrcRoot\"
SDK\Development\Src\AdventCoalition\Classes\X2DLCInfo_AdventCoalition.uc
```

The make commandlet searches for: `SDK\Development\Src\AdventCoalition\Classes\*.uc`  
C# puts files at: `SDK\Development\Src\AdventCoalition\AdventCoalition\Classes\`  
PowerShell puts files at: `SDK\Development\Src\AdventCoalition\Classes\` ✓

### Secondary Issue: Missing ItemGroup Regeneration

The PowerShell version regenerates the .x2proj file with detailed logging (~600 lines). The C# version either skips this entirely or does it silently without logging.

---

## Recommendations for Achieving Parity

### P0 - Critical (Must Fix)

1. **Fix mod source copy location**
   - Change `Copy Src folder to SDK` step to copy contents directly to `SDK\Development\Src\` not `SDK\Development\Src\ModName\`
   - Match PowerShell: `Copy-Item "$includeDir\*" "$devSrcRoot\"`

2. **Add ItemGroup regeneration logging**
   - Add detailed file-by-file logging to ProjectSynchronizer
   - Log "Removed Content/Folder..." and "Added Content/Folder..."
   - Log "Found X files and Y folders"
   - Log "Scanning project directory..."
   - Log "ItemGroup regeneration completed successfully"

3. **Fix logging consistency**
   - Replace `Console.WriteLine` with `_logger.LogInformation` in dependency copy step
   - Ensure all build output goes through ILogger for consistent capture

### P1 - Important (Should Fix)

4. **Add selective clean detailed logging**
   - Add "Selective cleaning of compiled scripts from {path} to avoid compiler error..."
   - Add "Cleaned." message after cleaning

5. **Add final copy detailed logging**
   - Add "Copying built mod to game directory..."
   - Add "Removing source staging directory {path} after successful move to {dest}..."
   - Add "Source directory removed. Move operation completed successfully."

6. **Add mod metadata logging**
   - Add "Reading mod metadata from {path}"
   - Add "Read."
   - Add "Writing mod metadata..."
   - Add "Written."

### P2 - Nice to Have

7. **Add content options detailed logging**
   - Match PowerShell's exact message format for missing content options

8. **Add shader precompilation logging**
   - Add "Checking the need to PrecompileShaders"
   - Match PowerShell's exact message format

---

## Conclusion

The C# implementation is **NOT at parity** with the PowerShell version. The claim of "100% feature parity" is **incorrect**.

**Critical issues:**
- Mod source files copied to wrong location → AdventCoalition.u NOT compiled
- ItemGroup regeneration missing → ~600 lines of logging absent
- Inconsistent logging → breaks log capture and comparison

**Build output comparison:**
- PowerShell: 885 lines, 30.70s, SUCCESS with AdventCoalition.u compiled
- C#: ~200 lines, 35.67s, FAILS with "Can't find files" warning

**Immediate action required:** Fix the mod source copy location (P0 #1) to achieve basic build functionality parity.
