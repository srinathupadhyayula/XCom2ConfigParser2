# XCom2ModCompiler - Architecture Document

## Document Information

| Attribute | Value |
|-----------|-------|
| **Version** | 1.0 |
| **Status** | Draft |
| **Created** | 2026-03-23 |
| **Last Updated** | 2026-03-23 |
| **Author** | XCom2Modding Community |
| **See Also** | [System Design Document](01_System_Design_Document.md), [Technical Design Document](02_Technical_Design_Document.md) |

---

## Table of Contents

1. [Architectural Goals](#1-architectural-goals)
2. [Architecture Patterns](#2-architecture-patterns)
3. [System Context](#3-system-context)
4. [Building Block View](#4-building-block-view)
5. [Runtime View](#5-runtime-view)
6. [Deployment View](#6-deployment-view)
7. [Cross-Cutting Concerns](#7-cross-cutting-concerns)
8. [Architecture Decisions](#8-architecture-decisions)
9. [Quality Requirements](#9-quality-requirements)
10. [Risks and Technical Debt](#10-risks-and-technical-debt)

---

## 1. Architectural Goals

### 1.1 Primary Objectives

| Objective | Description | Priority |
|-----------|-------------|----------|
| **O1: Compatibility** | Maintain 100% backwards compatibility with existing PowerShell workflows | Critical |
| **O2: Performance** | Match or exceed PowerShell build performance | High |
| **O3: Testability** | Enable comprehensive unit and integration testing | High |
| **O4: Maintainability** | Improve code quality and developer experience | Medium |
| **O5: Extensibility** | Enable future feature additions with minimal friction | Medium |

### 1.2 Quality Attribute Scenarios

#### Performance
```
Scenario: User builds a medium-sized mod (5000 lines of script, 100 assets)
Stimulus: User presses Ctrl+Alt+B
Response: Build completes in ≤ 60 seconds
Response Measure: Total build time
```

#### Modifiability
```
Scenario: Developer adds new build step
Stimulus: Code change required
Response: New step added without modifying existing pipeline
Response Measure: Lines of code changed < 50
```

#### Testability
```
Scenario: QA verifies incremental build detection
Stimulus: Run automated test suite
Response: All tests pass, coverage ≥ 80%
Response Measure: Code coverage percentage
```

#### Usability
```
Scenario: New user builds first mod
Stimulus: Follow documentation
Response: Successful build without errors
Response Measure: Support tickets < 5% of users
```

---

## 2. Architecture Patterns

### 2.1 Layered Architecture

The system follows a **layered architecture** with strict dependency rules:

```
┌─────────────────────────────────────────────────────────┐
│              Presentation Layer (CLI, PS, MSBuild)      │
│                        ↓ depends on                     │
├─────────────────────────────────────────────────────────┤
│              Application Layer (BuildController)        │
│                        ↓ depends on                     │
├─────────────────────────────────────────────────────────┤
│              Domain Layer (Compilation, Cooking, etc.)  │
│                        ↓ depends on                     │
├─────────────────────────────────────────────────────────┤
│           Infrastructure Layer (IO, Process, JSON)      │
└─────────────────────────────────────────────────────────┘
```

**Dependency Rule:** Upper layers may depend on lower layers. Lower layers must not depend on upper layers.

### 2.2 Ports and Adapters (Hexagonal)

Core business logic is isolated from external concerns:

```
                    ┌──────────────────┐
                    │   CLI Adapter    │
                    │   (Program.cs)   │
                    └────────┬─────────┘
                             │
                    ┌────────▼─────────┐
                    │  PS Adapter      │
                    │  (build.ps1)     │
                    └────────┬─────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        │           ┌────────▼────────┐           │
        │           │   BuildController  │◄───────┐
        │           │   (Application)  │         │
        │           └────────┬────────┘           │
        │                    │                    │
        │    ┌───────────────┼───────────────┐    │
        │    │               │               │    │
        │    ▼               ▼               ▼    │
        │  ┌───┐           ┌───┐           ┌───┐  │
        │  │Script│        │Asset│        │File │  │
        │  │Compiler│◄────►│Cooker│◄────►│Mirror│  │ Domain
        │  └───┘           └───┘           └───┘  │
        │                    │                    │
        └────────────────────┼────────────────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
        │    ┌───────────────┼───────────────┐    │
        │    ▼               ▼               ▼    │
        │  ┌───┐           ┌───┐           ┌───┐  │
        │  │Process│       │File │         │JSON │  │ Infrastructure
        │  │Runner │       │System│       │Serial│  │
        │  └───┘           └───┘           └───┘  │
        │                                         │
        └─────────────────── External ────────────┘
                             │
                    ┌────────▼─────────┐
                    │  XComGame.com    │
                    │  Robocopy.exe    │
                    │  File System     │
                    └──────────────────┘
```

### 2.3 Command Pattern

Build steps are encapsulated as commands:

```csharp
public interface IBuildStep
{
    string Description { get; }
    Task<StepResult> ExecuteAsync(BuildExecutionContext ctx, CancellationToken ct);
}

public class CopyModToSdkStep : IBuildStep
{
    public string Description => "Copying mod to SDK staging";
    
    public async Task<StepResult> ExecuteAsync(BuildExecutionContext ctx, CancellationToken ct)
    {
        // Implementation
    }
}
```

---

## 3. System Context

### 3.1 Context Diagram

```
┌──────────────────────────────────────────────────────────────────────┐
│                         XCom2ModCompiler System                      │
│                                                                      │
│  ┌────────────────────────────────────────────────────────────────┐  │
│  │                    XCom2ModCompiler Application                │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐  │  │
│  │  │ BuildController│ │ ScriptCompiler│ │ AssetCooker          │  │  │
│  │  └──────────────┘  └──────────────┘  └──────────────────────┘  │  │
│  └────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
         ▲                    │                    │
         │                    │                    │
         │                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│   VS Code /     │  │  XCOM 2 WOTC    │  │   Steam Steam   │
│   ModBuddy      │  │  SDK            │  │   Workshop       │
│                 │  │                 │  │                  │
│ - Build command │  │ - XComGame.com  │  │ - Mod destination│
│ - User config   │  │ - SrcOrig       │  │ - Content paths  │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

### 3.2 User Characteristics

| User Type | Technical Skill | Usage Pattern |
|-----------|----------------|---------------|
| **Mod Developer** | Medium | Daily builds, debugging |
| **Mod User** | Low | Install only, never build |
| **Build System Maintainer** | High | Modify build logic, fix bugs |
| **CI/CD Administrator** | Medium | Automated builds, release pipelines |

### 3.3 Operating Environment

| Component | Environment |
|-----------|-------------|
| **Runtime** | .NET 8.0 on Windows 10/11 |
| **IDE** | VS Code with C# extension, or Visual Studio |
| **SDK** | XCOM 2 War of the Chosen SDK |
| **Game** | XCOM 2 WOTC (Steam) |
| **Shell** | PowerShell 5.1+ |

---

## 4. Building Block View

### 4.1 Level 1: System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    XCom2ModCompiler System                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  White Box: XCom2ModCompiler.Application                        │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │  BuildController                                          │  │
│  │  - Main entry point for build orchestration               │  │
│  │  - Manages build lifecycle                                │  │
│  │  - Coordinates all build steps                            │  │
│  └───────────────────────────────────────────────────────────┘  │
│                                                                 │
│  White Box: XCom2ModCompiler.Domain                             │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌───────────┐ │
│  │ Compilation │ │   Cooking   │ │ Deployment  │ │ Tracking  │ │
│  │             │ │             │ │             │ │           │ │
│  │ - Script    │ │ - Asset     │ │ - File      │ │ - Build   │ │
│  │   Compiler  │ │   Cooker    │ │   Mirror    │ │   Tracker │ │
│  │ - Macro     │ │ - TFC       │ │ - Staging   │ │ - Fingerprint│
│  │   Validator │ │   Manager   │ │ - Mod       │ │ - Timestamp│
│  │             │ │             │ │   Metadata  │ │           │ │
│  └─────────────┘ └─────────────┘ └─────────────┘ └───────────┘ │
│                                                                 │
│  White Box: XCom2ModCompiler.Infrastructure                     │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌───────────┐ │
│  │  Process    │ │    File     │ │    JSON     │ │   Path    │ │
│  │  Runner     │ │  Operations │ │  Serializer │ │ Utilities │ │
│  └─────────────┘ └─────────────┘ └─────────────┘ └───────────┘ │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 Level 2: Domain Layer Decomposition

```
┌─────────────────────────────────────────────────────────────────┐
│              XCom2ModCompiler.Domain                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Compilation Subdomain:                                         │
│  ┌──────────────────┐  ┌──────────────────┐                    │
│  │ ScriptCompiler   │──│ OutputReceiver   │                    │
│  │                  │  │ (abstract)       │                    │
│  │ - CompileBase()  │  └────────┬─────────┘                    │
│  │ - CompileMod()   │           │                               │
│  │ - Validate()     │           ▼                               │
│  └──────────────────┘  ┌──────────────────┐                    │
│                        │ MakeOutputReceiver│                    │
│                        │ BufferingReceiver │                    │
│                        │ PassthroughReceiver│                   │
│                        └──────────────────┘                    │
│                                                                 │
│  Cooking Subdomain:                                             │
│  ┌──────────────────┐  ┌──────────────────┐                    │
│  │ AssetCooker      │──│ CookerOutput     │                    │
│  │                  │  │ Tracker          │                    │
│  │ - Cook()         │  │                  │                    │
│  │ - Clean()        │  │ - Track TFC      │                    │
│  │ - PrepareEngineIni│ │ - Track SF Pkgs  │                    │
│  └──────────────────┘  └──────────────────┘                    │
│                                                                 │
│  Deployment Subdomain:                                          │
│  ┌──────────────────┐  ┌──────────────────┐                    │
│  │ FileMirror       │──│ StagingManager   │                    │
│  │                  │  │                  │                    │
│  │ - Mirror()       │  │ - Stage Files    │                    │
│  │ - Copy()         │  │ - Final Copy     │                    │
│  │ - Delete()       │  │ - Cleanup        │                    │
│  └──────────────────┘  └──────────────────┘                    │
│                                                                 │
│  Tracking Subdomain:                                            │
│  ┌──────────────────┐  ┌──────────────────┐                    │
│  │ BuildTracker     │──│ MacroValidator   │                    │
│  │                  │  │                  │                    │
│  │ - Fingerprint    │  │ - Parse Macros   │                    │
│  │ - Change Detect  │  │ - Validate       │                    │
│  │ - Cache Mgmt     │  │ - Redefine Check │                    │
│  └──────────────────┘  └──────────────────┘                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 4.3 Level 3: Key Class Details

#### BuildController

```csharp
┌─────────────────────────────────────────────────────────┐
│                   BuildController                       │
├─────────────────────────────────────────────────────────┤
│ - options: BuildOptions                                 │
│ - logger: ILogger<BuildController>                      │
│ - tracker: BuildTracker                                 │
│ - compiler: ScriptCompiler                              │
│ - cooker: AssetCooker                                   │
│ - mirror: FileMirror                                    │
├─────────────────────────────────────────────────────────┤
│ + EnableDebug()                                         │
│ + EnableFinalRelease()                                  │
│ + SetWorkshopId(long)                                   │
│ + IncludeSrc(string)                                    │
│ + AddToClean(string)                                    │
│ + SetContentOptionsJson(string)                         │
│ + InvokeBuildAsync(CancellationToken)                   │
│ + InvokeCleanAsync(CancellationToken)                   │
└─────────────────────────────────────────────────────────┘
```

#### ScriptCompiler

```csharp
┌─────────────────────────────────────────────────────────┐
│                   ScriptCompiler                        │
├─────────────────────────────────────────────────────────┤
│ - commandletPath: string                                │
│ - sdkPath: string                                       │
│ - gamePath: string                                      │
│ - logger: ILogger<ScriptCompiler>                       │
├─────────────────────────────────────────────────────────┤
│ + CompileBaseAsync(BuildOptions, OutputReceiver, ct)    │
│ + CompileModAsync(string, string, BuildOptions, ...)    │
│ - BuildArguments(BuildOptions): string                  │
│ - InvokeCommandlet(string, OutputReceiver, ct): Task    │
└─────────────────────────────────────────────────────────┘
```

---

## 5. Runtime View

### 5.1 Build Process Flow

```
User (Ctrl+Alt+B)
       │
       ▼
┌─────────────────┐
│  VS Code Task   │
│  (tasks.json)   │
└────────┬────────┘
         │ powershell.exe build.ps1
         ▼
┌─────────────────┐
│  build.ps1      │
│  (PowerShell)   │
└────────┬────────┘
         │ Add-Type XCom2ModCompiler.dll
         │ BuildController.InvokeBuildAsync()
         ▼
┌─────────────────────────────────────────────────────────────────┐
│                    BuildController                              │
├─────────────────────────────────────────────────────────────────┤
│  Phase 1: Initialization                                        │
│  ├─► ValidateConfiguration()                                    │
│  ├─► InitializePaths()                                          │
│  └─► LoadContentOptions()                                       │
│                                                                  │
│  Phase 2: Preparation                                           │
│  ├─► RegenerateItemGroup()  [update .x2proj]                    │
│  ├─► CleanAdditional()      [if dependencies]                   │
│  ├─► CopyModToSdk()         [robocopy to staging]               │
│  └─► ConvertLocalization()  [UTF-8 to UTF-16]                   │
│                                                                  │
│  Phase 3: Script Compilation                                    │
│  ├─► CopyToSrc()            [mirror SrcOrig + dependencies]     │
│  ├─► RunPreMakeHooks()      [custom hooks]                      │
│  ├─► CheckCleanCompiled()   [incremental build detection]       │
│  ├─► RunMakeBase()          [XComGame.com make]                 │
│  └─► RunMakeMod()           [XComGame.com make -mods]           │
│                                                                  │
│  Phase 4: Post-Compilation                                        │
│  ├─► RecordCoreTimestamp()  [for change detection]              │
│  ├─► RunCookHL()            [if Highlander, cook native pkgs]   │
│  ├─► CopyScriptPackages()   [to staging]                        │
│  └─► CleanLeftoverScripts() [from SDK/game]                     │
│                                                                  │
│  Phase 5: Asset Processing                                      │
│  ├─► PrecompileShaders()    [shader cache]                      │
│  ├─► RunCookAssets()        [asset cooker]                      │
│  └─► CopyMissingUncooked()  [game packages]                     │
│                                                                  │
│  Phase 6: Finalization                                          │
│  └─► FinalCopy()            [staging to game/Mods]              │
│                                                                  │
│  Phase 7: Reporting                                             │
│  └─► ReportTimings()        [if enabled]                        │
└────────┬────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────┐
│  Build Result   │
│  (Success/Fail) │
└─────────────────┘
```

### 5.2 Sequence Diagram: Script Compilation

```
VS Code     build.ps1    BuildController   ScriptCompiler   XComGame.com
   │            │              │                 │                │
   │──build───▶│              │                 │                │
   │            │──Invoke────▶│                 │                │
   │            │  BuildAsync │                 │                │
   │            │              │                 │                │
   │            │              │──CopyToSrc────▶│                │
   │            │              │                 │                │
   │            │              │◄───────────────│                │
   │            │              │                 │                │
   │            │              │──Compile──────▶│                │
   │            │              │  (make args)    │                │
   │            │              │                 │──Process──────▶│
   │            │              │                 │                │
   │            │              │◄────────────────│◄───────────────│
   │            │              │  (exit code)    │  (output)      │
   │            │              │                 │                │
   │            │              │──Translate────▶│                │
   │            │              │  (paths)        │                │
   │            │              │                 │                │
   │            │◄─────────────│                 │                │
   │            │  Task        │                 │                │
   │            │  Complete    │                 │                │
   │◄───────────│              │                 │                │
   │  Result    │              │                 │                │
   │            │              │                 │                │
```

### 5.3 State Machine: Build State

```
┌──────────────┐
│   Created    │
└──────┬───────┘
       │ Configure()
       ▼
┌──────────────┐
│  Configured  │◄─────────────────────────┐
└──────┬───────┘                          │
       │ InvokeBuildAsync()               │
       ▼                                  │
┌──────────────┐     Error                │
│  Validating  │─────────────────────────▶│
└──────┬───────┘                          │
       │ Valid                            │
       ▼                                  │
┌──────────────┐     Error                │
│   Building   │─────────────────────────┤
└──────┬───────┘                          │
       │                                  │
       ├──────┬──────────┬────────────────│
       │      │          │                │
       ▼      ▼          ▼                ▼
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│ Success  │ │ Failure  │ │ Cancelled│ │ Crashed  │
└──────────┘ └──────────┘ └──────────┘ └──────────┘
```

---

## 6. Deployment View

### 6.1 Physical Deployment

```
┌─────────────────────────────────────────────────────────────────┐
│                    Developer Workstation                        │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              Development Environment                      │  │
│  │                                                           │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │  │
│  │  │   VS Code    │  │   ModBuddy   │  │  PowerShell  │   │  │
│  │  │              │  │  (Visual     │  │  Terminal    │   │  │
│  │  │  tasks.json  │  │   Studio)    │  │              │   │  │
│  │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘   │  │
│  │         │                 │                 │            │  │
│  │         └─────────────────┼─────────────────┘            │  │
│  │                           │                              │  │
│  │                           ▼                              │  │
│  │                  ┌─────────────────┐                     │  │
│  │                  │  build.ps1      │                     │  │
│  │                  │  (wrapper)      │                     │  │
│  │                  └────────┬────────┘                     │  │
│  │                           │                              │  │
│  │                           ▼                              │  │
│  │                  ┌─────────────────┐                     │  │
│  │                  │ XCom2ModCompiler│                     │  │
│  │                  │     .dll        │                     │  │
│  │                  └────────┬────────┘                     │  │
│  │                           │                              │  │
│  │         ┌─────────────────┼─────────────────┐            │  │
│  │         │                 │                 │            │  │
│  │         ▼                 ▼                 ▼            │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐   │  │
│  │  │  XCOM 2      │  │  XCOM 2      │  │  Steam       │   │  │
│  │  │  SDK         │  │  Game        │  │  Workshop    │   │  │
│  │  │              │  │              │  │  Content     │   │  │
│  │  │ - SrcOrig    │  │ - Mods       │  │              │   │  │
│  │  │ - Published  │  │ - Cooked     │  │ - Highlander │   │  │
│  │  │ - Binaries   │  │ - Script     │  │ - Dependencies│  │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘   │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 6.2 Directory Structure

```
D:\Projects\MyMod\
├── .scripts/
│   ├── build.ps1                    # PowerShell wrapper
│   ├── XCom2ModCompiler/
│   │   ├── XCom2ModCompiler.dll     # C# assembly
│   │   ├── XCom2ModCompiler.exe     # CLI executable
│   │   └── runtimeconfig.json       # .NET runtime config
│   └── X2ModBuildCommon/
│       ├── build_common.ps1         # Legacy (can be removed)
│       ├── clean.ps1                # Legacy (can be removed)
│       └── clean_cooker_output.ps1  # Legacy (can be removed)
│
├── MyMod/
│   ├── MyMod.x2proj                 # ModBuddy project
│   ├── Src/
│   │   └── MyPackage/
│   │       └── MyScript.uc
│   ├── Content/
│   │   └── MyAssets.upk
│   └── ContentForCook/
│       └── SeekFreePackages/
│
├── BuildCache/                      # Build artifacts
│   ├── lastBuildDetails.json
│   ├── AssetsCookerOutputTracker.json
│   └── CollectionMaps/
│
└── .vscode/
    └── tasks.json                   # VS Code build tasks
```

### 6.3 Runtime Dependencies

| Dependency | Location | Version |
|------------|----------|---------|
| **.NET 8.0 Runtime** | System PATH or bundled | 8.0.x |
| **PowerShell** | System32\WindowsPowerShell | 5.1+ |
| **XCOM 2 SDK** | User configured | Latest |
| **Robocopy** | System32 | Built-in |
| **XComGame.com** | SDK\binaries\Win64 | Game version |

---

## 7. Cross-Cutting Concerns

### 7.1 Logging

**Strategy:** Structured logging with multiple sinks.

```
Application
     │
     ▼
┌─────────────────┐
│ ILogger<T>      │
│ (Microsoft)     │
└────────┬────────┘
         │
         ├──────────────┬──────────────┬──────────────┐
         ▼              ▼              ▼              ▼
┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│  Console    │ │    File     │ │   Debug     │ │  Memory     │
│  (stdout)   │ │  (build.log)│ │  (Debugger) │ │  (Tests)    │
└─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘
```

**Log Levels:**
- **Trace:** Detailed diagnostic information (file only)
- **Debug:** Development debugging (file only)
- **Information:** Normal build progress (console + file)
- **Warning:** Non-fatal issues (console + file)
- **Error:** Build failures (console + file)
- **Critical:** System errors (console + file)

### 7.2 Configuration

**Configuration Sources (in priority order):**

1. **CLI Arguments** (highest priority)
2. **PowerShell Parameters**
3. **Environment Variables**
4. **User Config Files**
5. **Defaults** (lowest priority)

```csharp
public class ConfigurationBuilder
{
    public BuildOptions Build()
    {
        var options = new BuildOptions();
        
        // 1. Environment variables
        options.SdkPath = Environment.GetEnvironmentVariable("XCOM2_SDK_PATH") 
            ?? options.SdkPath;
        
        // 2. PowerShell parameters (passed to constructor)
        // 3. CLI arguments (parsed in Program.cs)
        
        return options;
    }
}
```

### 7.3 Exception Handling

**Strategy:** Fail fast with informative messages.

```
Exception Occurs
       │
       ▼
┌─────────────────┐
│ Catch Specific  │
│ BuildException  │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Log with        │
│ Context         │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Wrap in         │
│ BuildException  │
│ (if needed)     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Rethrow to      │
│ BuildController │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Report to User  │
│ (colored output)│
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Exit Code ≠ 0   │
└─────────────────┘
```

### 7.4 Security

| Concern | Mitigation |
|---------|------------|
| **Path Injection** | Validate all paths, use `Path.GetFullPath()` |
| **Command Injection** | Use `ProcessStartInfo` with arguments, no shell execution |
| **File Permissions** | Run with user permissions, no elevation required |
| **Assembly Loading** | Load from known locations only |

---

## 8. Architecture Decisions

### 8.1 Decision Log

| ID | Decision | Rationale | Status |
|----|----------|-----------|--------|
| **AD-001** | Use .NET 8.0 | LTS, performance, modern C# features | Approved |
| **AD-002** | Maintain PowerShell wrapper | VS Code compatibility | Approved |
| **AD-003** | Use Robocopy for file operations | Proven reliability, handles edge cases | Approved |
| **AD-004** | Async-first design | Better responsiveness, parallel operations | Approved |
| **AD-005** | Dependency injection | Testability, modifiability | Approved |
| **AD-006** | Structured logging | Debugging, diagnostics | Approved |
| **AD-007** | JSON for configuration | Human-readable, .NET support | Approved |

### 8.2 Key Decisions Detail

#### AD-001: Use .NET 8.0

**Problem:** Which .NET version to target?

**Options:**
1. .NET Framework 4.8 (existing ecosystem)
2. .NET 6.0 (previous LTS)
3. .NET 8.0 (current LTS)

**Decision:** .NET 8.0

**Rationale:**
- Latest LTS with long-term support
- Performance improvements over .NET 6
- Modern C# 12 features
- .NET Framework limits async/IO capabilities

**Consequences:**
- Requires .NET 8 runtime (minor deployment concern)
- Windows-only (not a concern for XCOM 2 modding)

#### AD-002: Maintain PowerShell Wrapper

**Problem:** How to integrate with VS Code?

**Options:**
1. Direct C# executable call
2. PowerShell wrapper script
3. MSBuild task only

**Decision:** PowerShell wrapper script

**Rationale:**
- Zero changes to existing tasks.json
- Preserves Ctrl+Alt+B workflow
- Allows gradual migration
- PowerShell handles environment setup

**Consequences:**
- Small overhead (negligible)
- Two technologies to maintain (mitigated by thin wrapper)

#### AD-003: Use Robocopy for File Operations

**Problem:** How to mirror directories?

**Options:**
1. Robocopy.exe wrapper
2. Pure .NET File.Copy
3. Third-party library

**Decision:** Robocopy.exe wrapper

**Rationale:**
- Battle-tested reliability
- Handles long paths, locked files
- Retry logic built-in
- Performance optimized at OS level

**Consequences:**
- Windows-only (acceptable)
- External process management required

---

## 9. Quality Requirements

### 9.1 Performance Requirements

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Build Time (small mod)** | < 30 seconds | Baseline test mod |
| **Build Time (medium mod)** | < 60 seconds | 5000 LOC, 100 assets |
| **Build Time (large mod)** | < 120 seconds | Highlander-scale |
| **File Copy Speed** | ≥ 100 MB/s | Robocopy benchmark |
| **Memory Usage** | < 500 MB peak | Process monitoring |

### 9.2 Reliability Requirements

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Build Success Rate** | ≥ 99% | CI/CD pipeline |
| **Crash Rate** | < 0.1% | Error telemetry |
| **Data Loss** | 0% | Staging safety checks |
| **Incremental Build Accuracy** | 100% | Change detection tests |

### 9.3 Maintainability Requirements

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Code Coverage** | ≥ 80% | Unit tests |
| **Cyclomatic Complexity** | < 15 avg | Static analysis |
| **Documentation Coverage** | 100% public API | XML docs |
| **Build Time (solution)** | < 10 seconds | MSBuild timing |

### 9.4 Usability Requirements

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Time to First Build** | < 5 minutes | New user setup |
| **Error Message Clarity** | ≥ 4/5 rating | User survey |
| **Documentation Findability** | < 30 seconds | Task-based search |

---

## 10. Risks and Technical Debt

### 10.1 Known Risks

| Risk | Probability | Impact | Mitigation | Owner |
|------|-------------|--------|------------|-------|
| **R1: Async deadlock in PowerShell** | Medium | High | Use `Task.Run()` wrapper | Dev Lead |
| **R2: TFC tracking logic errors** | High | High | Side-by-side comparison | QA |
| **R3: Path translation failures** | Medium | Medium | Comprehensive unit tests | Dev |
| **R4: Performance regression** | Medium | High | Benchmark suite | Dev Lead |
| **R5: User adoption resistance** | Low | Medium | Backwards compatibility | PM |

### 10.2 Technical Debt

| Debt | Impact | Repayment Plan | Priority |
|------|--------|----------------|----------|
| **TD-001: Initial PS-to-C# port** | Medium | Refactor after validation | Medium |
| **TD-002: Limited parallelization** | Low | Phase 2 optimization | Low |
| **TD-003: Basic error messages** | Medium | User feedback iteration | Medium |
| **TD-004: No CI/CD pipeline** | High | Setup GitHub Actions | High |

### 10.3 Architecture Smells to Avoid

| Smell | Prevention Strategy |
|-------|---------------------|
| **God Object** | Keep classes < 500 LOC |
| **Tight Coupling** | Dependency injection |
| **Leaky Abstractions** | Clean interface boundaries |
| **Premature Optimization** | Profile before optimizing |
| **Architecture by Accident** | Document decisions |

---

## Appendix A: Acronyms and Abbreviations

| Acronym | Meaning |
|---------|---------|
| **CLI** | Command Line Interface |
| **HL** | Highlander (mod type) |
| **LOC** | Lines of Code |
| **PS** | PowerShell |
| **SDK** | Software Development Kit |
| **SF** | Seek-Free (package format) |
| **TFC** | Texture File Cache |
| **UE3** | Unreal Engine 3 |
| **WOTC** | War of the Chosen (XCOM 2 expansion) |

---

## Appendix B: Architecture Review Checklist

- [ ] All quality requirements addressed
- [ ] All risks have mitigation plans
- [ ] Cross-cutting concerns handled consistently
- [ ] Interface boundaries are clean
- [ ] Dependencies follow allowed direction
- [ ] Logging strategy is implementable
- [ ] Exception handling is consistent
- [ ] Security concerns addressed
- [ ] Performance requirements achievable
- [ ] Documentation complete

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-23 | XCom2Modding Community | Initial draft |
