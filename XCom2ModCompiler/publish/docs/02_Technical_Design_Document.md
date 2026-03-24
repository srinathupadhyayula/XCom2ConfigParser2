# XCom2ModCompiler - Technical Design Document

## Document Information

| Attribute | Value |
|-----------|-------|
| **Version** | 1.0 |
| **Status** | Draft |
| **Created** | 2026-03-23 |
| **Last Updated** | 2026-03-23 |
| **Author** | XCom2Modding Community |
| **See Also** | [System Design Document](01_System_Design_Document.md), [Architecture Document](03_Architecture_Document.md) |

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Component Design](#2-component-design)
3. [Data Models](#3-data-models)
4. [Interface Design](#4-interface-design)
5. [Algorithm Design](#5-algorithm-design)
6. [Error Handling](#6-error-handling)
7. [Logging & Diagnostics](#7-logging--diagnostics)
8. [Testing Strategy](#8-testing-strategy)

---

## 1. Architecture Overview

### 1.1 Layered Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                       │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐  │
│  │  CLI (Program)  │  │  PowerShell     │  │  MSBuild    │  │
│  │                 │  │  Wrapper        │  │  Task       │  │
│  └─────────────────┘  └─────────────────┘  └─────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Application Layer                        │
│  ┌───────────────────────────────────────────────────────┐  │
│  │              BuildController (Facade)                 │  │
│  │  - InvokeBuildAsync()                                 │  │
│  │  - InvokeCleanAsync()                                 │  │
│  │  - Configuration methods                              │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     Domain Layer                            │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐        │
│  │   Script     │ │    Asset     │ │   Deployment │        │
│  │  Compilation │ │   Cooking    │ │   Pipeline   │        │
│  └──────────────┘ └──────────────┘ └──────────────┘        │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐        │
│  │    Build     │ │    Macro     │ │    Shader    │        │
│  │  Tracking    │ │  Validation  │ │  Precompile  │        │
│  └──────────────┘ └──────────────┘ └──────────────┘        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   Infrastructure Layer                      │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐        │
│  │   Process    │ │    File      │ │  JSON        │        │
│  │   Runner     │ │   Mirror     │ │  Serializer  │        │
│  └──────────────┘ └──────────────┘ └──────────────┘        │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐        │
│  │   Path       │ │  Timestamp   │ │   Output     │        │
│  │  Utilities   │ │   Tracker    │ │   Receiver   │        │
│  └──────────────┘ └──────────────┘ └──────────────┘        │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Design Patterns

| Pattern | Usage | Location |
|---------|-------|----------|
| **Facade** | `BuildController` simplifies build orchestration | Application Layer |
| **Strategy** | `OutputReceiver` variants for different output handling | Domain Layer |
| **Template Method** | `OutputReceiver.ParseLine()` template | Domain Layer |
| **Observer** | Process output event handling | Infrastructure Layer |
| **Builder** | `BuildOptions` construction | Domain Layer |
| **Repository** | `BuildTracker` for persistence | Domain Layer |

---

## 2. Component Design

### 2.1 BuildController

**Purpose:** Main orchestrator for the build pipeline.

**Responsibilities:**
- Coordinate all build steps
- Maintain build state
- Handle configuration options
- Report progress and results

**Public Interface:**
```csharp
public class BuildController
{
    // Construction
    public BuildController(BuildOptions options);
    
    // Configuration
    public void EnableDebug();
    public void EnableFinalRelease();
    public void SetWorkshopId(long id);
    public void IncludeSrc(string path);
    public void AddToClean(string modName);
    public void SetContentOptionsJson(string filename);
    
    // Execution
    public Task<BuildResult> InvokeBuildAsync(CancellationToken ct = default);
    public Task<CleanResult> InvokeCleanAsync(CancellationToken ct = default);
}
```

**Dependencies:**
- `ScriptCompiler`
- `AssetCooker`
- `FileMirror`
- `BuildTracker`
- `ILogger<BuildController>`

### 2.2 ScriptCompiler

**Purpose:** Orchestrate Unreal Engine make commandlet.

**Responsibilities:**
- Launch `XComGame.com` with correct arguments
- Parse and translate output paths
- Detect crashes and errors
- Handle debug/release modes

**Public Interface:**
```csharp
public class ScriptCompiler
{
    public ScriptCompiler(
        string commandletPath,
        string sdkPath,
        string gamePath,
        ILogger<ScriptCompiler> logger);
    
    public Task<CompilationResult> CompileBaseAsync(
        BuildOptions options,
        OutputReceiver receiver,
        CancellationToken ct);
    
    public Task<CompilationResult> CompileModAsync(
        string modName,
        string stagingPath,
        BuildOptions options,
        OutputReceiver receiver,
        CancellationToken ct);
}
```

**Key Algorithm:**
```csharp
private string BuildArguments(BuildOptions options)
{
    var args = new StringBuilder("make -nopause -unattended");
    
    if (options.FinalRelease)
        args.Append(" -final_release");
    
    if (options.Debug)
        args.Append(" -debug");
    
    return args.ToString();
}
```

### 2.3 AssetCooker

**Purpose:** Coordinate Unreal Engine asset cooking pipeline.

**Responsibilities:**
- Prepare Engine.ini modifications
- Manage TFC files
- Track cooker output
- Handle collection maps

**Public Interface:**
```csharp
public class AssetCooker
{
    public AssetCooker(
        string sdkPath,
        string gamePath,
        string buildCachePath,
        ContentOptions contentOptions,
        ILogger<AssetCooker> logger);
    
    public Task<CookResult> CookAsync(
        string modName,
        string contentForCookPath,
        string collectionMapsPath,
        CancellationToken ct);
    
    public Task CleanAsync(string modName, CancellationToken ct);
}
```

### 2.4 FileMirror

**Purpose:** Synchronize directories efficiently.

**Responsibilities:**
- Mirror source to staging
- Handle exclude patterns
- Preserve timestamps
- Report progress

**Public Interface:**
```csharp
public class FileMirror
{
    public FileMirror(ILogger<FileMirror> logger);
    
    public Task MirrorAsync(
        string source,
        string destination,
        string pattern = "*.*",
        string[]? excludeFiles = null,
        string[]? excludeDirs = null,
        CancellationToken ct = default);
    
    public Task CopyAsync(
        string source,
        string destination,
        bool overwrite = true,
        CancellationToken ct = default);
    
    public Task DeleteAsync(
        string path,
        bool recursive = true,
        CancellationToken ct = default);
}
```

**Implementation Options:**

| Approach | Pros | Cons | Recommendation |
|----------|------|------|----------------|
| **Robocopy.exe wrapper** | Proven, handles edge cases | External dependency | Use for production |
| **Pure .NET File.Copy** | No dependencies, testable | Slower, reinvents wheel | Use for unit tests |
| **Hybrid** | Best of both | More complex | **Recommended** |

### 2.5 BuildTracker

**Purpose:** Track build state for incremental builds.

**Responsibilities:**
- Store build fingerprints
- Detect configuration changes
- Track file timestamps
- Manage build cache

**Public Interface:**
```csharp
public class BuildTracker
{
    public BuildTracker(string cachePath, ILogger<BuildTracker> logger);
    
    public Task<BuildFingerprint> LoadFingerprintAsync();
    public Task SaveFingerprintAsync(BuildFingerprint fingerprint);
    
    public Task<bool> HasConfigurationChangedAsync(
        BuildOptions currentOptions,
        string globalsHash);
    
    public Task<bool> HasCorePackageChangedAsync(DateTime coreTimestamp);
}

public record BuildFingerprint(
    string BuildMode,
    string GlobalsHash,
    DateTime CoreTimestamp,
    DateTime LastBuildTime);
```

### 2.6 MacroValidator

**Purpose:** Detect macro redefinition conflicts.

**Responsibilities:**
- Parse `Globals.uci` and `extra_globals.uci`
- Track `define` directives
- Detect implicit redefinitions
- Allow explicit redefines

**Public Interface:**
```csharp
public class MacroValidator
{
    public MacroValidator(ILogger<MacroValidator> logger);
    
    public void ParseMacroFile(string filePath);
    public ValidationResult Validate();
    
    public void AllowRedefine(string macroName);
}

public record MacroDefinition(
    string Name,
    string FilePath,
    int LineNumber,
    bool IsExplicitRedefine);

public record ValidationResult(
    bool IsValid,
    IReadOnlyList<MacroError> Errors);

public record MacroError(
    string MacroName,
    string FilePath,
    int LineNumber,
    string Message);
```

### 2.7 OutputReceiver Hierarchy

**Purpose:** Handle process output with different strategies.

**Class Hierarchy:**
```
OutputReceiver (abstract)
├── PassthroughReceiver
├── BufferingReceiver
├── MakeOutputReceiver
└── ModCookReceiver
```

**Base Class:**
```csharp
public abstract class OutputReceiver
{
    public bool CrashDetected { get; protected set; }
    public string ProcessDescription { get; set; } = "";
    
    public abstract void ParseLine(string? line);
    public virtual void Finish(int exitCode)
    {
        if (CrashDetected)
            throw new BuildCrashException(ProcessDescription);
        
        if (exitCode != 0)
            throw new BuildFailureException(ProcessDescription, exitCode);
    }
}
```

**MakeOutputReceiver (Path Translation):**
```csharp
public class MakeOutputReceiver : OutputReceiver
{
    private readonly string[] _reversePaths;
    
    public override void ParseLine(string? line)
    {
        base.ParseLine(line);
        
        // Translate SDK paths to mod paths for error messages
        if (line is not null && ContainsErrorOrWarning(line))
        {
            var translated = TranslatePath(line);
            WriteColoredOutput(translated);
        }
        else
        {
            WriteColoredOutput(line);
        }
    }
    
    private string TranslatePath(string line)
    {
        // Pattern: path(line) : message
        var match = Regex.Match(line, @"^(.*)\((\d+)\)\s*:\s*(.*)$");
        if (!match.Success) return line;
        
        var origPath = match.Groups[1].Value;
        
        // Find actual file in reverse path order
        foreach (var checkPath in _reversePaths)
        {
            var testPath = origPath.Replace(SdkPathPattern, checkPath);
            if (File.Exists(testPath))
            {
                var fullPath = Path.GetFullPath(testPath);
                return line.Replace(origPath, fullPath);
            }
        }
        
        return line;
    }
}
```

---

## 3. Data Models

### 3.1 BuildOptions

```csharp
public class BuildOptions
{
    // Required
    public string ModName { get; init; } = "";
    public string ProjectRoot { get; init; } = "";
    public string SdkPath { get; init; } = "";
    public string GamePath { get; init; } = "";
    public string ModDestinationPath { get; init; } = "";
    
    // Configuration
    public bool Debug { get; set; }
    public bool FinalRelease { get; set; }
    public long WorkshopId { get; set; } = -1;
    
    // Dependencies
    public List<string> IncludePaths { get; } = new();
    public List<string> CleanMods { get; } = new();
    public string? ContentOptionsJson { get; set; }
    
    // Computed (set during initialization)
    public string ModNameCanonical => ModName.Replace(" ", "").Replace(";", "");
    public string ModSrcRoot => Path.Combine(ProjectRoot, ModName);
    public string StagingPath => Path.Combine(SdkPath, "XComGame", "Mods", ModNameCanonical);
    public string FinalModPath => Path.Combine(ModDestinationPath, ModNameCanonical);
    public string CookerOutputPath => Path.Combine(SdkPath, "XComGame", "Published", "CookedPCConsole");
    public string BuildCachePath => Path.Combine(ProjectRoot, "BuildCache");
    public string CommandletPath => Path.Combine(SdkPath, "binaries", "Win64", "XComGame.com");
}
```

### 3.2 ContentOptions

```csharp
public class ContentOptions
{
    public List<string> MissingUncooked { get; init; } = new();
    public List<string> SfStandalone { get; init; } = new();
    public List<string> SfMaps { get; init; } = new();
    public List<CollectionMapDefinition> SfCollectionMaps { get; init; } = new();
}

public class CollectionMapDefinition
{
    public string Name { get; init; } = "";
    public List<string> Packages { get; init; } = new();
}
```

### 3.3 CookerOutputTracker

```csharp
public class CookerOutputTracker
{
    public List<TfcFileData> TfcFiles { get; init; } = new();
    public List<SfPackageData> SfPackages { get; init; } = new();
}

public class TfcFileData
{
    public string FullFileName { get; init; } = "";
    public long OriginalSize { get; init; }
    public long LastUpdatedUtc { get; init; } // Ticks
}

public class SfPackageData
{
    public string FullFileName { get; init; } = "";
    public long LastUpdatedUtc { get; init; } // Ticks
}
```

### 3.4 BuildResult

```csharp
public record BuildResult(
    bool Success,
    TimeSpan Duration,
    List<string> OutputPaths,
    List<string> Errors,
    List<TimingRecord> Timings);

public record TimingRecord(
    string Description,
    double Seconds,
    string Share);

public record CleanResult(
    bool Success,
    List<string> DeletedPaths,
    List<string> Errors);
```

---

## 4. Interface Design

### 4.1 CLI Interface

```bash
# Build command
XCom2ModCompiler build \
  --mod-name "AdventCoalition" \
  --src-directory "D:\Projects\AdventCoalition" \
  --sdk-path "D:\Games\XCOM 2 War of the Chosen SDK" \
  --game-path "D:\Games\XCOM 2\XCom2-WaroftheChosen" \
  --mod-destination "C:\Program Files (x86)\Steam\steamapps\common\XCOM 2\XCom2-WarOfTheChosen\XComGame\Mods" \
  --config default

# Clean command
XCom2ModCompiler clean \
  --mod-name "AdventCoalition" \
  --src-directory "D:\Projects\AdventCoalition" \
  --sdk-path "D:\Games\XCOM 2 War of the Chosen SDK"

# Options
--config <default|debug>       Build configuration
--final-release                Enable final release mode
--workshop-id <id>             Override Steam Workshop ID
--include-src <path>           Add dependency source path
--clean-mod <name>             Add mod to clean list
--content-options <file>       ContentOptions.json path
--verbose                      Enable verbose logging
--timing-report                Show build timing report
```

### 4.2 PowerShell Interface

```powershell
# build.ps1 (thin wrapper)
Param(
    [string] $srcDirectory,
    [string] $sdkPath,
    [string] $gamePath,
    [string] $config,
    [string] $modDestinationPath
)

$ErrorActionPreference = "Stop"

# Load C# assembly
$assemblyPath = Join-Path $PSScriptRoot "XCom2ModCompiler.dll"
Add-Type -Path $assemblyPath

# Create options
$options = [XCom2ModCompiler.BuildOptions]::new()
$options.ProjectRoot = $srcDirectory
$options.SdkPath = $sdkPath
$options.GamePath = $gamePath
$options.ModDestinationPath = $modDestinationPath
$options.ModName = "AdventCoalition"

# Apply configuration
if ($config -eq "debug") {
    $options.Debug = $true
}

# Execute build
$controller = [XCom2ModCompiler.BuildController]::new($options)
$controller.InvokeBuildAsync().Wait()
```

### 4.3 MSBuild Interface (XCOM2.targets)

```xml
<Target Name="Build">
    <InvokePowershellTask
        EntryPs1="$(BuildCommonRoot)build.ps1"
        SolutionRoot="$(MSBuildProjectDirectory)"
        SdkInstallPath="$(XCOM2_UserPath)..\"
        GameInstallPath="$(XCOM2_GamePath)..\"
        AdditionalArgs="@(Args)"
    />
</Target>
```

---

## 5. Algorithm Design

### 5.1 Incremental Build Detection

```csharp
public async Task<bool> ShouldRebuildAsync(BuildOptions options)
{
    var fingerprint = await _tracker.LoadFingerprintAsync();
    
    // Check build mode switch
    var currentMode = options.Debug ? "debug" : "release";
    if (fingerprint.BuildMode != currentMode)
    {
        _logger.LogInformation("Detected switch between debug and release build");
        return true;
    }
    
    // Check Globals.uci hash
    var globalsPath = Path.Combine(options.SdkPath, "Development", "Src", "Core", "Globals.uci");
    var currentHash = await ComputeFileHashAsync(globalsPath);
    if (fingerprint.GlobalsHash != currentHash)
    {
        _logger.LogInformation("Detected change in macros (Globals.uci)");
        return true;
    }
    
    // Check Core.u timestamp
    var corePath = Path.Combine(options.SdkPath, "XComGame", "Script", "Core.u");
    if (File.Exists(corePath))
    {
        var coreTime = File.GetLastWriteTime(corePath);
        if (fingerprint.CoreTimestamp != coreTime)
        {
            _logger.LogInformation("Detected external rebuild");
            return true;
        }
    }
    
    return false;
}
```

### 5.2 TFC Growth Detection

```csharp
public List<TfcGrowthRecord> DetectTfcGrowth(List<FileInfo> currentTfcs)
{
    var growthRecords = new List<TfcGrowthRecord>();
    
    foreach (var tfc in currentTfcs)
    {
        var tracked = _tracker.GetTfcData(tfc.Name);
        if (tracked == null)
            continue; // New file, ignore
        
        if (tfc.Length == tracked.OriginalSize)
            continue; // No change
        
        var increase = (double)tfc.Length / tracked.OriginalSize;
        growthRecords.Add(new TfcGrowthRecord(
            Name: tfc.Name,
            OriginalSize: FormatFileSize(tracked.OriginalSize),
            CurrentSize: FormatFileSize(tfc.Length),
            Increase: $"{increase:F2}x"
        ));
    }
    
    return growthRecords;
}
```

### 5.3 Process Tree Killing

```csharp
public static void KillProcessTree(int pid)
{
    try
    {
        // Get all child processes
        var children = GetChildProcesses(pid);
        
        // Kill children first (recursive)
        foreach (var child in children)
        {
            KillProcessTree(child.Id);
        }
        
        // Kill parent
        using var process = Process.GetProcessById(pid);
        process.Kill();
        process.WaitForExit(5000);
    }
    catch (ArgumentException)
    {
        // Process already exited - ignore
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to kill process {pid}: {ex.Message}");
    }
}

private static List<Process> GetChildProcesses(int pid)
{
    var children = new List<Process>();
    
    try
    {
        var searcher = new ManagementObjectSearcher(
            $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {pid}");
        
        foreach (var obj in searcher.Get())
        {
            var childPid = (uint)obj["ProcessId"];
            children.Add(Process.GetProcessById((int)childPid));
        }
    }
    catch
    {
        // WMI query failed - return empty list
    }
    
    return children;
}
```

---

## 6. Error Handling

### 6.1 Exception Hierarchy

```
Exception
└── BuildException (abstract)
    ├── BuildCrashException        # Process crashed
    ├── BuildFailureException      # Non-zero exit code
    ├── BuildConfigurationException # Invalid configuration
    ├── BuildPathException         # Path not found / invalid
    └── BuildIOException           # File I/O error
```

### 6.2 Exception Definitions

```csharp
public abstract class BuildException : Exception
{
    public BuildException(string message) : base(message) { }
    public BuildException(string message, Exception inner) : base(message, inner) { }
}

public class BuildCrashException : BuildException
{
    public string ProcessDescription { get; }
    
    public BuildCrashException(string processDescr)
        : base($"Crash detected while {processDescr}")
    {
        ProcessDescription = processDescr;
    }
}

public class BuildFailureException : BuildException
{
    public int ExitCode { get; }
    
    public BuildFailureException(string processDescr, int exitCode)
        : base($"Failed {processDescr} (exit code {exitCode})")
    {
        ExitCode = exitCode;
    }
}

public class BuildConfigurationException : BuildException
{
    public string ConfigurationKey { get; }
    
    public BuildConfigurationException(string key, string message)
        : base($"Configuration error for '{key}': {message}")
    {
        ConfigurationKey = key;
    }
}

public class BuildPathException : BuildException
{
    public string Path { get; }
    
    public BuildPathException(string path, string reason)
        : base($"Path '{path}' is invalid: {reason}")
    {
        Path = path;
    }
}
```

### 6.3 Error Handling Strategy

```csharp
public async Task<BuildResult> InvokeBuildAsync(CancellationToken ct)
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        await ValidateConfigurationAsync();
        await InitializeAsync(ct);
        
        // Build pipeline
        await CopyModToSdkAsync(ct);
        await ConvertLocalizationAsync(ct);
        
        if (ShouldCompileBase())
        {
            await CopyToSrcAsync(ct);
            await RunPreMakeHooksAsync();
            
            if (await ShouldRebuildAsync())
            {
                await SelectiveCleanAsync(ct);
            }
            
            await RunMakeBaseAsync(ct);
        }
        
        if (HasScriptPackages())
        {
            await RunMakeModAsync(ct);
            
            if (IsHighlander && !Options.Debug)
            {
                await RunCookHighlanderAsync(ct);
            }
            
            await CopyScriptPackagesAsync(ct);
        }
        
        await PrecompileShadersAsync(ct);
        await RunCookAssetsAsync(ct);
        await CopyMissingUncookedAsync(ct);
        await FinalCopyAsync(ct);
        
        stopwatch.Stop();
        return BuildResult.Success(stopwatch.Elapsed, _timings);
    }
    catch (BuildException ex)
    {
        _logger.LogError(ex, "Build failed: {Message}", ex.Message);
        return BuildResult.Failure(ex.Message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected build failure: {Message}", ex.Message);
        return BuildResult.Failure($"Unexpected error: {ex.Message}");
    }
}
```

---

## 7. Logging & Diagnostics

### 7.1 Logging Configuration

```csharp
public static class LoggingExtensions
{
    public static ILoggingBuilder AddBuildLogging(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.AddConsole(options =>
        {
            options.TimestampFormat = "HH:mm:ss ";
            options.LogToStandardErrorThreshold = LogLevel.Warning;
        });
        builder.SetMinimumLevel(LogLevel.Information);
        
        // Verbose logging for debugging
        if (Environment.GetEnvironmentVariable("X2MC_VERBOSE") == "1")
        {
            builder.SetMinimumLevel(LogLevel.Debug);
        }
        
        return builder;
    }
}
```

### 7.2 Log Message Categories

| Category | Level | Examples |
|----------|-------|----------|
| **Build** | Info | "Starting build", "Build completed" |
| **File** | Debug | "Copying file X to Y", "Deleting Z" |
| **Process** | Info | "Compiling scripts", "Cooking assets" |
| **Error** | Error | "Compilation failed", "Path not found" |
| **Timing** | Info | "Completed in 45.23s" |

### 7.3 Timing Report

```csharp
public void ReportTimings(List<TimingRecord> timings, TimeSpan totalDuration)
{
    if (Environment.GetEnvironmentVariable("X2MC_REPORT_TIMINGS") != "1")
        return;
    
    var accountedTime = timings.Sum(t => t.Seconds);
    timings.Add(new TimingRecord("Total Duration", totalDuration.TotalSeconds, ""));
    timings.Add(new TimingRecord("Unaccounted Time", totalDuration.TotalSeconds - accountedTime, ""));
    
    var report = timings
        .OrderByDescending(t => t.Seconds)
        .Select(t => new
        {
            t.Description,
            Time = $"{t.Seconds:F2}s",
            Share = $"{(t.Seconds / totalDuration.TotalSeconds):P1}"
        });
    
    Console.WriteLine(TableFormatter.Format(report));
}
```

---

## 8. Testing Strategy

### 8.1 Test Categories

| Category | Scope | Tools | Target Coverage |
|----------|-------|-------|-----------------|
| **Unit Tests** | Individual classes | xUnit, Moq | 80% |
| **Integration Tests** | Component interactions | xUnit, TestSdk | 60% |
| **End-to-End Tests** | Full build pipeline | PowerShell scripts | 100% critical paths |
| **Performance Tests** | Build time benchmarks | BenchmarkDotNet | N/A |

### 8.2 Unit Test Examples

```csharp
public class MacroValidatorTests
{
    [Fact]
    public void ParseMacroFile_ValidFile_NoErrors()
    {
        // Arrange
        var validator = new MacroValidator(NullLogger<MacroValidator>.Instance);
        var testFile = CreateTempFile(@"
            `define MY_MACRO 1
            `define ANOTHER_MACRO 2
        ");
        
        // Act
        validator.ParseMacroFile(testFile);
        var result = validator.Validate();
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
    
    [Fact]
    public void ParseMacroFile_ImplicitRedefine_HasError()
    {
        // Arrange
        var validator = new MacroValidator(NullLogger<MacroValidator>.Instance);
        var file1 = CreateTempFile("`define MY_MACRO 1");
        var file2 = CreateTempFile("`define MY_MACRO 2");
        
        // Act
        validator.ParseMacroFile(file1);
        validator.ParseMacroFile(file2);
        var result = validator.Validate();
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors, e => e.MacroName == "MY_MACRO");
    }
    
    [Fact]
    public void ParseMacroFile_ExplicitRedefine_NoError()
    {
        // Arrange
        var validator = new MacroValidator(NullLogger<MacroValidator>.Instance);
        var file1 = CreateTempFile("`define MY_MACRO 1");
        var file2 = CreateTempFile(@"
            // X2MBC-Redefine
            `define MY_MACRO 2
        ");
        
        // Act
        validator.ParseMacroFile(file1);
        validator.ParseMacroFile(file2);
        var result = validator.Validate();
        
        // Assert
        Assert.True(result.IsValid);
    }
}
```

### 8.3 Integration Test Setup

```csharp
public class BuildControllerIntegrationTests : IDisposable
{
    private readonly string _testSdkPath;
    private readonly string _testGamePath;
    private readonly string _testProjectPath;
    
    public BuildControllerIntegrationTests()
    {
        // Create test SDK structure
        _testSdkPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestSdkStructure(_testSdkPath);
        
        // Create test game structure
        _testGamePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestGameStructure(_testGamePath);
        
        // Create test mod project
        _testProjectPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        CreateTestModProject(_testProjectPath);
    }
    
    [Fact]
    public async Task InvokeBuildAsync_SimpleMod_BuildsSuccessfully()
    {
        // Arrange
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = _testProjectPath,
            SdkPath = _testSdkPath,
            GamePath = _testGamePath,
            ModDestinationPath = Path.Combine(_testGamePath, "XComGame", "Mods")
        };
        
        var controller = new BuildController(options);
        
        // Act
        var result = await controller.InvokeBuildAsync();
        
        // Assert
        Assert.True(result.Success);
        Assert.FileExists(Path.Combine(options.FinalModPath, "TestMod.XComMod"));
    }
    
    public void Dispose()
    {
        // Cleanup test directories
        DeleteDirectory(_testSdkPath);
        DeleteDirectory(_testGamePath);
        DeleteDirectory(_testProjectPath);
    }
}
```

### 8.4 End-to-End Test Script

```powershell
# test-e2e.ps1
param(
    [string] $TestModPath,
    [string] $SdkPath,
    [string] $GamePath
)

$ErrorActionPreference = "Stop"

Write-Host "=== E2E Test: Default Build ==="
& "$TestModPath\.scripts\build.ps1" `
    -srcDirectory $TestModPath `
    -sdkPath $SdkPath `
    -gamePath $GamePath `
    -config default `
    -modDestinationPath "$GamePath\XComGame\Mods"

if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED: Default build" -ForegroundColor Red
    exit 1
}

Write-Host "=== E2E Test: Debug Build ==="
& "$TestModPath\.scripts\build.ps1" `
    -srcDirectory $TestModPath `
    -sdkPath $SdkPath `
    -gamePath $GamePath `
    -config debug

if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED: Debug build" -ForegroundColor Red
    exit 1
}

Write-Host "=== E2E Test: Clean ==="
& "$TestModPath\.scripts\X2ModBuildCommon\clean.ps1" `
    -SolutionRoot $TestModPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "FAILED: Clean" -ForegroundColor Red
    exit 1
}

Write-Host "=== All E2E Tests Passed ===" -ForegroundColor Green
```

---

## Appendix A: File Structure

```
XCom2ModCompiler/
├── XCom2ModCompiler.csproj
├── Program.cs
├── BuildController.cs
├── Configuration/
│   ├── BuildOptions.cs
│   ├── ContentOptions.cs
│   └── UserConfig.cs
├── Compilation/
│   ├── ScriptCompiler.cs
│   ├── MacroValidator.cs
│   └── OutputReceivers.cs
├── Cooking/
│   ├── AssetCooker.cs
│   ├── TfcManager.cs
│   └── CookerOutputTracker.cs
├── Deployment/
│   ├── FileMirror.cs
│   ├── StagingManager.cs
│   └── ModMetadata.cs
├── Tracking/
│   ├── BuildTracker.cs
│   └── TimestampTracker.cs
├── Utilities/
│   ├── ProcessExtensions.cs
│   ├── PathUtilities.cs
│   └── TableFormatter.cs
├── Exceptions/
│   ├── BuildException.cs
│   ├── BuildCrashException.cs
│   └── BuildFailureException.cs
└── docs/
    ├── 01_System_Design_Document.md
    ├── 02_Technical_Design_Document.md
    └── 03_Architecture_Document.md
```

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-23 | XCom2Modding Community | Initial draft |
