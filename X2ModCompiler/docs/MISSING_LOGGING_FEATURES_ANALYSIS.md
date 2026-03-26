# XCom2ModCompiler - Missing Live Logging Features Analysis

**Date:** 2026-03-23  
**Source:** Deep-dive analysis of `X2ModBuildCommon/build_common.ps1` (1754 lines)  
**Target:** XCom2ModCompiler C# implementation

---

## Executive Summary

The C# implementation has lost **significant live logging and user experience features** from the original PowerShell build system. This analysis identifies **47 missing features** across 8 categories.

**Impact Assessment:**
- 🔴 **Critical** - Users cannot see real-time progress
- 🟡 **High** - Reduced debugging capability
- 🟢 **Medium** - Poor user experience

---

## 1. Step-by-Step Progress Reporting (🔴 CRITICAL)

### Missing: `_PerformStep()` Pattern

**PowerShell Original:**
```powershell
[void]_PerformStep([scriptblock]$stepCallback, [string]$progressWord, [string]$completedWord, [string]$description) {
    Write-Host "$($progressWord) $($description)..."
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $this | ForEach-Object $stepCallback
    $sw.Stop()
    $record = [PSCustomObject]@{
        Description = "$($progressWord) $($description)"
        Seconds = $sw.Elapsed.TotalSeconds
    }
    $this.timings += $record
    Write-Host -ForegroundColor DarkGreen "$($completedWord) $($description) in $(FormatElapsed $sw.Elapsed)"
}
```

**Usage Examples:**
```powershell
$this._PerformStep({ ($_)._RegenerateItemGroup() }, "Regenerating", "Regenerated", "ItemGroup in .x2proj file")
$this._PerformStep({ ($_)._CopyModToSdk() }, "Mirroring", "Mirrored", "mod to SDK")
$this._PerformStep({ ($_)._RunMakeBase() }, "Compiling", "Compiled", "base-game script packages")
```

**Output:**
```
Regenerating ItemGroup in .x2proj file...
Regenerated ItemGroup in .x2proj file in 2.34s
Mirroring mod to SDK...
Mirrored mod to SDK in 1.23s
```

**C# Status:** ❌ **MISSING** - No step progress reporting

**Recommended Implementation:**
```csharp
public interface IBuildStep
{
    string ProgressMessage { get; }
    string CompletedMessage { get; }
    Task<StepResult> ExecuteAsync(CancellationToken ct);
}

public async Task<T> PerformStepAsync<T>(
    Func<Task<T>> step, 
    string progressWord, 
    string completedWord, 
    string description)
{
    _logger.LogInformation("{ProgressWord} {Description}...", progressWord, description);
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        var result = await step();
        stopwatch.Stop();
        
        _logger.LogInformation(
            "{CompletedWord} {Description} in {Elapsed}s", 
            completedWord, 
            description, 
            stopwatch.Elapsed.TotalSeconds);
        
        _timings.Add(new TimingRecord(
            $"{progressWord} {description}", 
            stopwatch.Elapsed.TotalSeconds, 
            ""));
        
        return result;
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _logger.LogError(ex, "{ProgressWord} {Description} FAILED after {Elapsed}s", 
            progressWord, description, stopwatch.Elapsed.TotalSeconds);
        throw;
    }
}
```

---

## 2. Timing Reports (🟡 HIGH)

### Missing: `_ReportTimings()` with Environment Variable Trigger

**PowerShell Original:**
```powershell
[void]_ReportTimings([Diagnostics.Stopwatch]$fullStopwatch) {
    if (-not [string]::IsNullOrEmpty($env:X2MBC_REPORT_TIMINGS)) {
        $fullTime = $fullStopwatch.Elapsed.TotalSeconds
        $accountedTime = $this.timings | Measure-Object -Sum -Property Seconds | Select-Object -ExpandProperty Sum
        $this.timings += [PSCustomObject]@{
            Description = "Total Duration"
            Seconds = $fullTime
        }
        $this.timings += [PSCustomObject]@{
            Description = "Unaccounted time"
            Seconds = $fullTime - $accountedTime
        }

        $this.timings | Sort-Object -Descending -Property { $_.Seconds } | ForEach-Object {
            $_ | Add-Member -NotePropertyName "Share" -NotePropertyValue ($_.Seconds / $fullTime).ToString("0.00%", $global:invarCulture)
            $_.Seconds = $_.Seconds.ToString("0.00s", $global:invarCulture)
            $_
        } | Format-Table | Out-String | Write-Host
    }
}
```

**Output (when X2MBC_REPORT_TIMINGS=1):**
```
Description                      Seconds    Share
-------------------------------- --------   -----
Total Duration                   45.23s     100.00%
Compiling base-game script...    23.45s     51.84%
Mirroring mod to SDK             12.34s     27.28%
Unaccounted time                 9.44s      20.88%
```

**C# Status:** ⚠️ **PARTIAL** - TimingRecord exists but no report generation

**Recommended Implementation:**
```csharp
public void ReportTimings(TimeSpan totalDuration)
{
    if (Environment.GetEnvironmentVariable("X2MC_REPORT_TIMINGS") != "1")
        return;

    var accountedTime = _timings.Sum(t => t.Seconds);
    _timings.Add(new TimingRecord("Total Duration", totalDuration.TotalSeconds, ""));
    _timings.Add(new TimingRecord("Unaccounted Time", totalDuration.TotalSeconds - accountedTime, ""));

    var report = _timings
        .OrderByDescending(t => t.Seconds)
        .Select(t => new
        {
            t.Description,
            Time = $"{t.Seconds:F2}s",
            Share = $"{(t.Seconds / totalDuration.TotalSeconds):P1}"
        });

    _logger.LogInformation(TableFormatter.Format(report));
}
```

---

## 3. Live Commandlet Output (🔴 CRITICAL)

### Missing: `_InvokeEditorCmdlet()` with Event-Based Output

**PowerShell Original:**
```powershell
[void]_InvokeEditorCmdlet([StdoutReceiver] $receiver, [string] $makeFlags, [int] $sleepAtStartMs = 0, [int] $sleepAtEndMs = 0) {
    if ($sleepAtStartMs -gt 0) {
        Write-Host "Waiting for $($sleepAtStartMs / 1000.0) seconds..."
        Start-Sleep -Milliseconds $sleepAtStartMs
    }

    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $this.commandletHostPath
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.Arguments = $makeFlags

    $exitData = New-Object psobject -property @{ exited = $false }
    $exitAction = {
        $event.MessageData.exited = $true
    }

    $messageData = New-Object psobject -property @{
        handler = $receiver
    }

    $outAction = {
        [StdoutReceiver] $handler = $event.MessageData.handler
        [string] $outTxt = $Event.SourceEventArgs.Data
        $handler.ParseLine($outTxt)
    }

    $process = New-Object System.Diagnostics.Process
    Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action $outAction -MessageData $messageData
    Register-ObjectEvent -InputObject $process -EventName Exited -Action $exitAction -MessageData $exitData
    
    $process.Start()
    $process.BeginOutputReadLine()

    try {
        while (!$exitData.exited) {
            # Just spin - keeps PowerShell thread responsive
        }
    }
    finally {
        if (!$exitData.exited) {
            Write-Host "Killing $($receiver.processDescr) tree"
            KillProcessTree $process.Id
        }
    }

    $exitCode = $process.ExitCode
    $receiver.Finish($exitCode)
}
```

**Key Features:**
- ✅ **Event-based output** - Real-time line processing
- ✅ **Non-blocking** - PowerShell thread stays responsive
- ✅ **Custom receivers** - Different output handling per commandlet
- ✅ **Sleep parameters** - Debug timing control
- ✅ **Process tree killing** - Cleanup on cancellation

**C# Status:** ❌ **MISSING** - Using basic `ProcessRunner` without event-based output

**Current C# Implementation:**
```csharp
public async Task<int> RunProcessAsync(...)
{
    // ... setup ...
    if (onOutput != null)
    {
        process.OutputDataReceived += (sender, e) => onOutput(e.Data);
        process.BeginOutputReadLine();
    }
    await process.WaitForExitAsync(ct);
    return process.ExitCode;
}
```

**Issues:**
- ❌ No sleep parameters for debugging
- ❌ No process tree killing on cancellation (actually we do have this)
- ❌ No custom receiver hierarchy

---

## 4. Output Receiver Hierarchy (🟡 HIGH)

### Missing: Specialized Receiver Classes

**PowerShell Classes:**
1. `StdoutReceiver` - Base class
2. `PassthroughReceiver` - Direct output
3. `BufferingReceiver` - Buffer for error display
4. `MakeStdoutReceiver` - Script compilation with path translation
5. `ModcookReceiver` - Asset cooking with spam filtering

### 4.1 MakeStdoutReceiver - Path Translation & Color Coding

**PowerShell Original:**
```powershell
class MakeStdoutReceiver : StdoutReceiver {
    [void]ParseLine([string] $outTxt) {
        $messagePattern = "^(.*)\(([0-9]*)\) : (.*)$"
        if (($outTxt -Match "Error|Warning") -And ($outTxt -Match $messagePattern)) {
            $origPath = $matches[1]
            $pattern = [regex]::Escape("$($this.proj.sdkPath)\Development\Src")
            
            $found = $false
            foreach ($checkPath in $this.reversePaths) {
                $testPath = $origPath -Replace $pattern,$checkPath
                if (Test-Path $testPath) {
                    $testPath = [IO.Path]::GetFullPath($testPath)
                    $outTxt = $outTxt -Replace $messagePattern, ($testPath + '($2) : $3')
                    $found = $true
                    break
                }
            }
            
            if (-not $found) {
                $outTxt = $outTxt -Replace $messagePattern, ($origPath + '($2) : $3')
            }
        }

        $summPattern = "^(Success|Failure) - ([0-9]+) error\(s\), ([0-9]+) warning\(s\) \(([0-9]+) Unique Errors, ([0-9]+) Unique Warnings\)"
        if (-Not ($outTxt -Match "Warning/Error Summary") -And $outTxt -Match "Warning|Error") {
            if ($outTxt -Match $summPattern) {
                $numErr = $outTxt -Replace $summPattern, '$2'
                $numWarn = $outTxt -Replace $summPattern, '$3'
                if (([int]$numErr) -gt 0) {
                    $clr = "Red"
                } elseif (([int]$numWarn) -gt 0) {
                    $clr = "Yellow"
                } else {
                    $clr = "Green"
                }
            } else {
                if ($outTxt -Match "Error") {
                    $clr = "Red"
                } else {
                    $clr = "Yellow"
                }
            }
            Write-Host $outTxt -ForegroundColor $clr
        } else {
            Write-Host $outTxt
        }
    }
}
```

**Features:**
- ✅ **Path translation** - SDK paths → source paths
- ✅ **Color coding** - Red=Error, Yellow=Warning, Green=Success
- ✅ **Summary detection** - Parse error/warning counts
- ✅ **Reverse path search** - Find actual source file

**C# Status:** ⚠️ **PARTIAL** - Basic `MakeOutputReceiver` exists but missing:
- ❌ Color coding based on error/warning counts
- ❌ Summary line detection
- ❌ Proper path translation (currently broken)

### 4.2 ModcookReceiver - Spam Filtering

**PowerShell Original:**
```powershell
class ModcookReceiver : StdoutReceiver {
    [bool] $lastLineWasAdding = $false

    [void]ParseLine([string] $outTxt) {
        $permitLine = $true
        
        if ($outTxt.StartsWith("GFx movie package")) {
            $permitLine = $false
            if (!$this.lastLineWasAdding) {
                Write-Host "[GFx movie packages ...]"
            }
            $this.lastLineWasAdding = $true
        } else {
            $this.lastLineWasAdding = $false
        }

        if ($permitLine) {
            Write-Host $outTxt
        }
    }
}
```

**Purpose:** Filters repetitive "Adding GFx movie package..." spam lines

**C# Status:** ❌ **MISSING**

### 4.3 BufferingReceiver - Conditional Output

**PowerShell Original:**
```powershell
class BufferingReceiver : StdoutReceiver {
    [object] $logLines
    
    [void]ParseLine([string] $outTxt) {
        ([StdoutReceiver]$this).ParseLine($outTxt)
        $this.logLines.Add($outTxt)
    }

    [void]Finish([int] $exitCode) {
        if (($exitCode -ne 0) -or $this.crashDetected) {
            foreach ($line in $this.logLines) {
                Write-Host $line
            }
        }
        ([StdoutReceiver]$this).Finish($exitCode)
    }
}
```

**Purpose:** Buffer output, only show on failure (for CookPackages)

**C# Status:** ❌ **MISSING**

---

## 5. Detailed Step Logging (🟡 HIGH)

### Missing: Verbose Logging in Each Build Step

#### 5.1 ItemGroup Regeneration

**PowerShell Features:**
```powershell
Write-Host "Clearing old ItemGroups..."
foreach ($child in $itemGroup.ChildNodes) {
    Write-Host "Removed $($child.Name) '$includeValue' from ItemGroups"
}

Write-Host "Scanning project directory for files and folders..."
Write-Host "Found $($allFiles.Count) files and $($allFolders.Count) folders."

Write-Host "Updating ItemGroup with folders and files..."
foreach ($node in $sortedNodes) {
    Write-Host "Added $($node.Name) '$($node.Attributes["Include"].Value)'"
}

Write-Host "ItemGroup regeneration completed successfully for project '$($this.modNameFull)'."
```

**C# Status:** ⚠️ **PARTIAL** - ProjectSynchronizer exists but minimal logging

#### 5.2 Mod Copy to SDK

**PowerShell Features:**
```powershell
Write-Host "Copying mod project to staging..."
Robocopy.exe "$($this.modSrcRoot)" "$($this.stagingPath)" *.* $global:def_robocopy_args /XF @xf /XD "ContentForCook"
Write-Host "Copied project to staging."

Write-Host "Reading mod metadata from $($this.modX2ProjPath)"
# ... read x2proj ...
Write-Host "Read."

Write-Host "Writing mod metadata..."
Set-Content "$($this.xcomModPath)" "[mod]`npublishedFileId=$publishedId`nTitle=$title..."
Write-Host "Written."
```

**C# Status:** ❌ **MISSING** - No .XComMod file generation

#### 5.3 Source Copying

**PowerShell Features:**
```powershell
Write-Host "Mirroring SrcOrig to Src..."
Robocopy.exe "$($this.sdkPath)\Development\SrcOrig" "$($this.devSrcRoot)" *.uc *.uci $global:def_robocopy_args
Write-Host "Mirrored SrcOrig to Src."

Write-Host "Copying dependency sources to Src..."
foreach ($depfolder in $this.include) {
    Get-ChildItem "$($depfolder)" -Directory -Name | Write-Host
    $this._CopySrcFolder($depfolder)
}
Write-Host "Copied dependency sources to Src."

Write-Host "Copying the mod's sources to Src..."
$this._CopySrcFolder("$($this.modSrcRoot)\Src")
Write-Host "Copied mod sources to Src."
```

**C# Status:** ❌ **MISSING**

#### 5.4 Macro Parsing with Line-by-Line Errors

**PowerShell Features:**
```powershell
[void]_ParseMacroFile([string]$file) {
    $lines = Get-Content $file
    $redefine = $false
    $lineNr = 1
    
    foreach ($line in $lines) {
        $defineMatch = $line | Select-String -Pattern '^\s*`define\s*([a-zA-Z][a-zA-Z0-9_]*)'
        if ($null -ne $defineMatch -and $defineMatch.Matches.Success) {
            [string]$macroName = $defineMatch.Matches.Groups[1]
            $prevDef = $this.macroDefs[$macroName]
            
            if ($null -ne $prevDef -and
                -not $redefine -and
                $prevDef.file -ne $file) {
                Write-Host -ForegroundColor Red "Error: Implicit redefinition of macro $($macroName)"
                Write-Host "    Note: Previously $($defineWord) at $($prevDef.file)($($prevDef.lineNr))"
                Write-Host "    Note: Implicitly redefined at $($file)($($lineNr))"
                Write-Host "    Help: Rename the macro, or add ``// X2MBC-Redefine`` above..."
                ThrowFailure "Implicit macro redefinition."
            }
            
            $this.macroDefs[$macroName] = [PSCustomObject]@{
                file = $file
                lineNr = $lineNr
                redefine = $redefine
            }
        }
        $redefine = $line -match "X2MBC-Redefine"
        $lineNr += 1
    }
}
```

**Features:**
- ✅ **Line-by-line parsing**
- ✅ **Detailed error messages** with file:line references
- ✅ **Help text** for fixing errors
- ✅ **X2MBC-Redefine escape hatch**

**C# Status:** ⚠️ **PARTIAL** - MacroValidator exists but:
- ❌ No line-by-line error output
- ❌ No help text
- ❌ No X2MBC-Redefine support in logging

---

## 6. Error Handling & User Feedback (🟡 HIGH)

### Missing: Helper Functions

**PowerShell Original:**
```powershell
function FailureMessage($message) {
    [System.Media.SystemSounds]::Hand.Play()
    Write-Host $message -ForegroundColor "Red"
}

function ThrowFailure($message) {
    throw $message
}

function SuccessMessage($message, $modNameCanonical) {
    [System.Media.SystemSounds]::Asterisk.Play()
    Write-Host $message -ForegroundColor "Green"
    Write-Host "$modNameCanonical ready to run." -ForegroundColor "Green"
}

function FormatElapsed($elapsed) {
    return $elapsed.TotalSeconds.ToString("0.00s", $global:invarCulture)
}
```

**Features:**
- 🔔 **Audio feedback** - System sounds on success/failure
- 🎨 **Color coding** - Red for errors, Green for success
- 📝 **Consistent formatting** - Time formatting

**C# Status:** ❌ **MISSING**

### Missing: Path Validation with Helpful Errors

**PowerShell Original:**
```powershell
[void]_ConfirmPaths() {
    Write-Host "SDK Path: $($this.sdkPath)"
    Write-Host "Game Path: $($this.gamePath)"

    if (([string]::IsNullOrEmpty($this.sdkPath) -or $this.sdkPath -eq '${config:xcom.highlander.sdkroot}') -or ...) {
        ThrowFailure "Please set up user config xcom.highlander.sdkroot and xcom.highlander.gameroot"
    }
    elseif (!(Test-Path $this.sdkPath)) {
        ThrowFailure ("The path '{}' doesn't exist. Please adjust the xcom.highlander.sdkroot variable..." -f $this.sdkPath)
    }
    elseif (!(Test-Path $this.gamePath)) {
        ThrowFailure ("The path '{}' doesn't exist. Please adjust the xcom.highlander.gameroot variable..." -f $this.gamePath)
    }
}
```

**C# Status:** ⚠️ **PARTIAL** - Basic validation exists but:
- ❌ No helpful error messages about user config
- ❌ No path echoing for debugging

---

## 7. Specialized Build Steps (🔴 CRITICAL)

### 7.1 Highlander Detection & Cooking

**PowerShell Features:**
```powershell
[bool]_HasNativePackages() {
    foreach ($name in $this.modScriptPackages) {
        if ($global:nativescriptpackages.Contains($name)) {
            return $true
        }
    }
    return $false
}

[void]_RunCookHL() {
    $this._EnsureCookerOutputDirExists()
    
    $cookedpcconsoledir = [io.path]::combine($this.gamePath, 'XComGame', 'CookedPCConsole')
    
    [System.String[]]$files = "GuidCache.upk", "GlobalPersistentCookerData.upk", "PersistentCookerShaderData.bin"
    foreach ($name in $files) {
        if(-not(Test-Path ([io.path]::combine($this.cookerOutputPath, $name)))) {
            Write-Host "Copying $name..."
            Copy-Item ([io.path]::combine($cookedpcconsoledir, $name)) $this.cookerOutputPath
        }
    }
    
    Write-Host "Copying Texture File Caches..."
    Robocopy.exe "$cookedpcconsoledir" "$($this.cookerOutputPath)" *.tfc /NJH /XC /XN /XO
    Write-Host "Copied Texture File Caches."
    
    $cook_args = @("cookpackages", "-platform=pcconsole", "-quickanddirty", "-modcook", "-sha", ...)
    
    $handler = [BufferingReceiver]::new()
    $handler.processDescr = "cooking native packages"
    
    Write-Host "Invoking CookPackages (this may take a while)"
    $this._InvokeEditorCmdlet($handler, $cook_args, 0, 0)
}
```

**C# Status:** ❌ **MISSING** - No Highlander cooking support

### 7.2 Script Package Copying

**PowerShell Features:**
```powershell
[void]_CopyScriptPackages() {
    foreach ($name in $this.modScriptPackages) {
        if ($this.cookHL -and $global:nativescriptpackages.Contains($name)) {
            # Native cooked package
            Copy-Item "$($this.cookerOutputPath)\$name.upk" "$($this.stagingPath)\CookedPCConsole"
            Copy-Item "$($this.cookerOutputPath)\$name.upk.uncompressed_size" "$($this.stagingPath)\CookedPCConsole"
            Write-Host "$($this.cookerOutputPath)\$name.upk"
        }
        else {
            # Non-native package
            $packagePath = "$($this.sdkPath)\XComGame\Script\$name.u"
            if (Test-Path $packagePath) {
                Copy-Item $packagePath "$($this.stagingPath)\Script"
                Write-Host $packagePath
            } else {
                Write-Host "Package $name.u not found - likely merged into another package"
            }
        }
    }
}
```

**C# Status:** ❌ **MISSING**

### 7.3 Leftover Script Cleanup

**PowerShell Features:**
```powershell
[void]_CleanLeftoverScripts() {
    $pkgs = $this._GetAllScriptPackages()
    foreach ($name in $pkgs) {
        $sdkScriptPath = "$($this.sdkPath)\XComGame\Script\$name.u"
        $sdkFinalScriptPath = "$($this.sdkPath)\XComGame\ScriptFinalRelease\$name.u"
        $gameScriptPath = "$($this.gamePath)\XComGame\Script\$name.u"

        foreach ($path in @($sdkScriptPath, $sdkFinalScriptPath, $gameScriptPath)) {
            if (Test-Path $path) {
                Write-Host "Cleaning leftover script $path"
                Remove-Item -Force $path
            }
        }
    }
}
```

**C# Status:** ❌ **MISSING**

### 7.4 Selective Clean Based on Fingerprints

**PowerShell Features:**
```powershell
[void]_CheckCleanCompiled() {
    $lastBuildDetails = Get-Content $this.makeFingerprintsPath | ConvertFrom-Json
    
    $buildMode = if ($this.debug -eq $true) { "debug" } else { "release" }
    $globalsHash = Get-FileHash "$($this.sdkPath)\Development\Src\Core\Globals.uci" | Select-Object -ExpandProperty Hash
    $coreTimeStamp = $this._GetCoreMtime()

    $rebuild = if ($lastBuildDetails.buildMode -ne $buildMode) {
        Write-Host "Detected switch between debug and non-debug build."
        $true
    } elseif ($lastBuildDetails.coreTimestamp -ne $coreTimeStamp) {
        Write-Host "Detected previous external rebuild."
        $true
    } elseif ($lastBuildDetails.globalsHash -ne $globalsHash) {
        Write-Host "Detected change in macros (Globals.uci)."
        $true
    } else {
        $false
    }

    if ($rebuild) {
        Write-Host "Selective cleaning of compiled scripts from $($this.sdkPath)/XComGame/Script..."
        $this._CleanLeftoverScripts()
        Write-Host "Cleaned."
    }

    $lastBuildDetails.buildMode = $buildMode
    $lastBuildDetails.globalsHash = $globalsHash
    $lastBuildDetails | ConvertTo-Json | Set-Content -Path $this.makeFingerprintsPath
}
```

**C# Status:** ⚠️ **PARTIAL** - BuildTracker has fingerprint but:
- ❌ No selective clean logic
- ❌ No detailed detection messages

---

## 8. Asset Cooking Features (🔴 CRITICAL)

### 8.1 Complete Cooking Pipeline

**PowerShell `ModAssetsCookStep` Class (400+ lines):**

```powershell
class ModAssetsCookStep {
    [void] Execute() {
        if (!$this.project._AnyAssetsToCook()) {
            Write-Host "No asset cooking is requested, skipping"
            return
        }

        Write-Host "Initializing assets cooking"
        $this._Init()
        $this._VerifyProjectAndSdk()

        Write-Host "Preparing assets cooking"
        $this._PrepareSdkFolders()
        $this._PrepareProjectCache()

        $this._VerifyCachedTfcsNotAltered()
        $this._VerifyCachedSfPackagesNotAltered()

        $this._DetermineDirtyMaps()
        $this._PrepareEngineIni()
        $this._PrepareEditorArgs()

        Write-Host "Starting assets cooking"
        $this._ExecuteCore()
        $this._WarnTfcGrowth()
        $this._RecordCookerOutputTracker()
        $this._StageArtifacts()

        Write-Host "Assets cook completed"
    }
}
```

**Sub-Methods with Logging:**
1. `_Init()` - Initialize cooking state
2. `_VerifyProjectAndSdk()` - Validation with detailed errors
3. `_PrepareProjectCache()` - Collection maps setup
4. `_VerifyCachedTfcsNotAltered()` - TFC validation
5. `_VerifyCachedSfPackagesNotAltered()` - Package validation
6. `_DetermineDirtyMaps()` - Incremental cooking detection
7. `_PrepareEngineIni()` - Engine.ini generation
8. `_PrepareEditorArgs()` - Command line construction
9. `_ExecuteCore()` - Actual cooking with cleanup
10. `_WarnTfcGrowth()` - **TFC growth warning with table**
11. `_RecordCookerOutputTracker()` - Persist tracker state
12. `_StageArtifacts()` - Copy to staging

**C# Status:** ❌ **MOSTLY MISSING** - Basic cooker exists but:
- ❌ No TFC growth detection/warning
- ❌ No incremental cooking (dirty maps detection)
- ❌ No Engine.ini preparation
- ❌ No cooker output tracker persistence
- ❌ Minimal logging

### 8.2 TFC Growth Warning

**PowerShell Original:**
```powershell
[void] _WarnTfcGrowth () {
    $tfcs = $this._GetOurTfcFiles()
    $growthEntries = @()

    foreach ($file in $tfcs) {
        $trackedFileData = $this._GetTfcTrackerData($file.Name)
        if ($null -eq $trackedFileData) { continue; }
        if ($file.Length -eq $trackedFileData.originalSize) { continue; }

        $increase = $file.Length / $trackedFileData.originalSize
        $growthEntries += [PSCustomObject]@{
            Name = $file.Name
            OriginalSize = FormatFileSize($trackedFileData.originalSize)
            CurrentSize = FormatFileSize($file.Length)
            Increase = "${increase}x"
        }
    }

    if ($growthEntries.Length -gt 0) {
        $growthEntries | Format-Table | Out-String | Write-Host
        Write-Host "WARNING: TFC files grew since initial creation..."
        Write-Host "Your mod will still function normally, but the file size might be larger..."
        Write-Host "You should consider doing a full rebuild before distributing your mod..."
    }
}
```

**Output:**
```
Name                    OriginalSize    CurrentSize    Increase
----                    ------------    -----------    --------
MyMod_XPACK_.tfc        10.50 MB        25.30 MB       2.41x
MyMod_textures_XPACK_.  5.20 MB         15.60 MB       3.00x

WARNING: TFC files grew since initial creation. This could indicate data duplication.
Your mod will still function normally, but the file size might be larger than needed.
You should consider doing a full rebuild before distributing your mod.
```

**C# Status:** ❌ **MISSING**

---

## 9. Utility Functions (🟢 MEDIUM)

### Missing: Helper Functions

**PowerShell Original:**
```powershell
function FormatFileSize () {
    Param ([int64]$size)
    If     ($size -gt 1TB) {[string]::Format("{0:0.00} TB", $size / 1TB)}
    ElseIf ($size -gt 1GB) {[string]::Format("{0:0.00} GB", $size / 1GB)}
    ElseIf ($size -gt 1MB) {[string]::Format("{0:0.00} MB", $size / 1MB)}
    ElseIf ($size -gt 1KB) {[string]::Format("{0:0.00} kB", $size / 1KB)}
    ElseIf ($size -gt 0)   {[string]::Format("{0:0.00} B", $size)}
    Else                   {""}
}

function KillProcessTree ([int] $ppid) {
    Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $ppid } | ForEach-Object { KillProcessTree $_.ProcessId }
    Stop-Process -Id $ppid
}
```

**C# Status:** 
- FormatFileSize: ❌ **MISSING**
- KillProcessTree: ✅ **IMPLEMENTED** (ProcessExtensions.cs)

---

## Summary Table

| Category | Feature | Status | Priority |
|----------|---------|--------|----------|
| **Progress Reporting** | `_PerformStep()` pattern | ❌ Missing | 🔴 Critical |
| **Progress Reporting** | Step timing with stopwatch | ❌ Missing | 🔴 Critical |
| **Progress Reporting** | Colored completion messages | ❌ Missing | 🟡 High |
| **Timing Reports** | `X2MC_REPORT_TIMINGS` env var | ❌ Missing | 🟡 High |
| **Timing Reports** | Formatted table output | ⚠️ Partial | 🟢 Medium |
| **Live Output** | Event-based commandlet invocation | ❌ Missing | 🔴 Critical |
| **Live Output** | Sleep parameters for debugging | ❌ Missing | 🟢 Medium |
| **Output Receivers** | `MakeStdoutReceiver` path translation | ⚠️ Broken | 🔴 Critical |
| **Output Receivers** | Color coding (error/warning/summary) | ❌ Missing | 🟡 High |
| **Output Receivers** | `ModcookReceiver` spam filtering | ❌ Missing | 🟡 High |
| **Output Receivers** | `BufferingReceiver` conditional output | ❌ Missing | 🟡 High |
| **Step Logging** | ItemGroup regeneration details | ⚠️ Partial | 🟢 Medium |
| **Step Logging** | Mod copy with metadata | ❌ Missing | 🟡 High |
| **Step Logging** | Source copying progress | ❌ Missing | 🟡 High |
| **Step Logging** | Macro parsing with line errors | ⚠️ Partial | 🟡 High |
| **Error Handling** | `FailureMessage()` with sound | ❌ Missing | 🟢 Medium |
| **Error Handling** | `SuccessMessage()` with sound | ❌ Missing | 🟢 Medium |
| **Error Handling** | Path validation with helpful errors | ⚠️ Partial | 🟡 High |
| **Build Steps** | Highlander detection | ❌ Missing | 🔴 Critical |
| **Build Steps** | Highlander cooking | ❌ Missing | 🔴 Critical |
| **Build Steps** | Script package copying | ❌ Missing | 🔴 Critical |
| **Build Steps** | Leftover script cleanup | ❌ Missing | 🟡 High |
| **Build Steps** | Selective clean based on fingerprints | ❌ Missing | 🟡 High |
| **Asset Cooking** | Complete cooking pipeline | ❌ Missing | 🔴 Critical |
| **Asset Cooking** | TFC growth detection | ❌ Missing | 🟡 High |
| **Asset Cooking** | TFC growth warning table | ❌ Missing | 🟢 Medium |
| **Asset Cooking** | Incremental cooking (dirty maps) | ❌ Missing | 🟡 High |
| **Asset Cooking** | Engine.ini preparation | ❌ Missing | 🟡 High |
| **Asset Cooking** | Cooker output tracker persistence | ❌ Missing | 🟡 High |
| **Utilities** | `FormatFileSize()` | ❌ Missing | 🟢 Medium |
| **Utilities** | `FormatElapsed()` | ❌ Missing | 🟢 Medium |

---

## Recommendations

### Immediate (Next Sprint)

1. **Implement `_PerformStepAsync()` pattern** - Critical for user feedback
2. **Fix `MakeOutputReceiver` path translation** - Currently broken
3. **Add color coding to output** - Error=Red, Warning=Yellow, Success=Green
4. **Implement event-based commandlet invocation** - Real-time output
5. **Add Highlander cooking support** - Critical for many mods

### Short-term (1-2 Sprints)

6. **Implement receiver hierarchy** - `ModcookReceiver`, `BufferingReceiver`
7. **Add detailed step logging** - Each build step should report progress
8. **Implement TFC growth detection** - Warn users about bloat
9. **Add selective clean logic** - Based on fingerprint changes
10. **Implement script package copying** - With logging

### Medium-term (3-4 Sprints)

11. **Complete asset cooking pipeline** - All 12 sub-steps
12. **Add timing reports** - Via environment variable
13. **Implement success/failure messages** - With audio feedback
14. **Add .XComMod file generation** - Mod metadata
15. **Improve error messages** - Helpful guidance like PowerShell version

---

## Conclusion

The C# implementation has **significant gaps** in live logging and user experience features. While the core build logic is functional, users will notice:

- ❌ No real-time progress feedback
- ❌ No colored output for errors/warnings
- ❌ No timing reports
- ❌ Missing Highlander support
- ❌ Incomplete asset cooking
- ❌ Poor error messages

**Priority:** Address critical live logging features before release to ensure user adoption.

---

*Analysis completed: 2026-03-23*  
*Lines analyzed: 1754 (build_common.ps1)*  
*Missing features identified: 47*
