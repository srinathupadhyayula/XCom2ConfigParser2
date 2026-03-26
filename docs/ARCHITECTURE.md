## Part 1: System Architecture

### 1.1 High-Level System Context

```
┌─────────────────────────────────────────────────────────────────┐
│                     XCOM 2 Mod Development                       │
│                                                                  │
│  ┌──────────────┐         ┌──────────────┐                     │
│  │   Mod Author │         │  XCOM 2 SDK  │                     │
│  │   (User)     │         │              │                     │
│  └──────┬───────┘         └──────┬───────┘                     │
│         │                        │                              │
│         │ 1. Write Code          │                              │
│         │    (.uc files)         │                              │
│         │                        │                              │
│         │ 2. Write Config        │                              │
│         │    (.ini files)        │                              │
│         │                        │                              │
│         ▼                        │                              │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │           XCom2ConfigParser2 (Standalone Tool)           │   │
│  │  ┌──────────────────────────────────────────────────┐  │   │
│  │  │  CLI: dotnet XCom2ConfigParser2.dll --settings   │  │   │
│  │  │                                                   │  │   │
│  │  │  • Tokenizes .ini files                          │  │   │
│  │  │  • Validates UE3 syntax                          │  │   │
│  │  │  • Validates struct members against .uc defs     │  │   │
│  │  │  • Outputs errors/warnings                       │  │   │
│  │  └──────────────────────────────────────────────────┘  │   │
│  └─────────────────────────────────────────────────────────┘   │
│         │                                                      │
│         │ 3. Build Mod                                         │
│         ▼                                                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │            XCom2ModCompiler (Build System)               │   │
│  │  ┌──────────────────────────────────────────────────┐  │   │
│  │  │  CLI: XCom2ModCompiler.exe build --mod-name X    │  │   │
│  │  │                                                   │  │   │
│  │  │  • Modular Pipeline (IBuildStep architecture)     │  │   │
│  │  │  • Compiles UnrealScript (.uc → .u)             │  │   │
│  │  │  • Cooks assets (.umap → .umap)                 │  │   │
│  │  │  • Native File Mirroring (Zero Robocopy)          │  │   │
│  │  │  • Incremental builds with BuildTracker           │  │   │
│  │  └──────────────────────────────────────────────────┘  │   │
│  └─────────────────────────────────────────────────────────┘   │
│         │                                                      │
│         │ 4. Deploy Mod                                        │
│         ▼                                                      │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              XCOM 2 Game / Workshop                      │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 XCom2ConfigParser2 Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    XCom2ConfigParser2                                │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Program.cs (CLI Entry Point)                                │  │
│  │  ┌────────────────────────────────────────────────────────┐ │  │
│  │  │  ParseCommand                                          │ │  │
│  │  │  • Parse CLI arguments (Spectre.Console.Cli)          │ │  │
│  │  │  • Load settings from .vscode/settings.json           │ │  │
│  │  │  • Orchestrate validation pipeline                    │ │  │
│  │  │  • Format output (console/JSON/log file)              │ │  │
│  │  └────────────────────────────────────────────────────────┘ │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  CLI/ (Command-Line Interface Layer)                         │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │  │
│  │  │ FileProcessor│  │OutputFormatter│  │ ErrorLog    │      │  │
│  │  │              │  │              │  │ Writer       │      │  │
│  │  │ • Process    │  │ • Default    │  │              │  │  │
│  │  │   file       │  │ • JSON       │  │ • Text       │  │  │
│  │  │ • Validate   │  │ • Summary    │  │ • JSON       │  │  │
│  │  │ • Aggregate  │  │ • Quiet      │  │ • Categorized│  │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘      │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Validation Layer                                            │  │
│  │  ┌──────────────────────────────────────────────────────┐   │  │
│  │  │  SyntaxValidator (IValidator)                        │   │  │
│  │  │  • Validate section headers [SectionName]            │   │  │
│  │  │  • Validate KVP syntax Property=Value                │   │  │
│  │  │  • Validate value types (bool, number, struct)       │   │  │
│  │  │  • Regex-based validation                            │   │  │
│  │  └──────────────────────────────────────────────────────┘   │  │
│  │  ┌──────────────────────────────────────────────────────┐   │  │
│  │  │  StructMemberValidator                               │   │  │
│  │  │  • Phase 1: Resolve variable type from section/prop  │   │  │
│  │  │  • Phase 2: Resolve struct definition                │   │  │
│  │  │  • Validate struct member names                      │   │  │
│  │  │  • Two-phase resolution with caching                 │   │  │
│  │  └──────────────────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Parser Layer                                                │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │  │
│  │  │ LineSplitter │  │DirectiveTok. │  │ StructParser │      │  │
│  │  │              │  │              │  │              │      │  │
│  │  │ • Handle \\  │  │ • Tokenize   │  │ • Parse      │  │  │
│  │  │ • Merge      │  │   into       │  │   (Name=Val, │  │  │
│  │  │   lines      │  │   directives │  │   Array=(..))│  │  │
│  │  │              │  │              │  │              │      │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘      │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  StructValidation/ (UnrealScript Integration)                │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │  │
│  │  │UnrealScript  │  │  ClassFile   │  │  Struct      │      │  │
│  │  │Parser        │  │  Locator     │  │  Indexer     │      │  │
│  │  │              │  │              │  │              │      │  │
│  │  │ • Parse .uc  │  │ • Find .uc   │  │ • Pre-index  │  │  │
│  │  │   files for  │  │   files in   │  │   all structs│  │  │
│  │  │   struct     │  │   Src folders│  │   from .uc   │  │  │
│  │  │   definitions│  │              │  │   files      │  │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘      │  │
│  │  ┌──────────────┐  ┌──────────────┐                        │  │
│  │  │ StructCache  │  │ VariableCache│                        │  │
│  │  │ (disk-backed)│  │ (disk-backed)│                        │  │
│  │  └──────────────┘  └──────────────┘                        │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Core/ (Data Structures)                                     │  │
│  │  Directive, Kvp, SectionHeader, Diagnostic, Span, etc.       │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Configuration/                                              │  │
│  │  ┌──────────────────────────────────────────────────────┐   │  │
│  │  │  SettingsLoader + ParserSettings                     │   │  │
│  │  │  • Load .vscode/settings.json                        │   │  │
│  │  │  • Resolve paths (iniRoots, localSrcRoot, etc.)      │   │  │
│  │  │  • Parse build.ps1 for dependencies                  │   │  │
│  │  └──────────────────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘

Data Flow:
.ini file → LineSplitter → DirectiveTokenizer → SyntaxValidator → StructMemberValidator → Diagnostics
```

### 1.3 XCom2ModCompiler Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                      XCom2ModCompiler                                │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Program.cs (CLI Entry Point)                                │  │
│  │  ┌────────────────────────────────────────────────────────┐ │  │
│  │  │  BuildCommand / CleanCommand                           │ │  │
│  │  │  • Parse CLI arguments (Spectre.Console.Cli)          │ │  │
│  │  │  • BuildController instantiation                      │ │  │
│  │  │  • Invoke build/clean pipeline                        │ │  │
│  │  └────────────────────────────────────────────────────────┘ │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Application/ (Build Orchestration)                          │  │
│  │  ┌──────────────────────────────────────────────────────┐   │  │
│  │  BuildController                                         │   │  │
│  │                                                       │   │  │
│  │  Build Pipeline (IBuildStep):                         │   │  │
│  │  1. ProjectSyncStep (Synchronize .x2proj)             │   │  │
│  │  2. StagingStep (Mirror to staging)                   │   │  │
│  │  3. MetadataStep (Generate .XComMod)                  │   │  │
│  │  4. CleanupStep (Selective clean / Cache cleanup)     │   │  │
│  │  5. CompilationStep (Invoke ScriptCompiler)           │   │  │
│  │  6. CookingStep (Invoke AssetCooker)                  │   │  │
│  │  7. UncookedCopyStep (Copy missing uncooked)          │   │  │
│  │  8. ShaderStep (Precompile shaders)                   │   │  │
│  │  9. MirrorStep (Final deployment)                     │   │  │
│  │  10. ValidationStep (Integrated Config Validation)    │   │  │
│  │  • BuildTracker (State management & Fingerprints)     │   │  │
│  │  └──────────────────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                       │
│              ┌───────────────┼───────────────┐                      │
│              ▼               ▼               ▼                      │
│  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐   │
│  │  Compilation/    │ │  Cooking/        │ │  Tracking/       │   │
│  │                  │ │                  │ │                  │   │
│  │ • ScriptCompiler │ │ • AssetCooker    │ │ • BuildTracker   │   │
│  │ • OutputReceiver │ │ • ModAssetsCook  │ │ • MacroValidator │   │
│  │ • ShaderPrecomp. │ │   Step           │ │ • CookerOutput   │   │
│  │ • BuildResult    │ │                  │ │   Tracker        │   │
│  └──────────────────┘ └──────────────────┘ └──────────────────┘   │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Utilities/ (Helper Classes)                                 │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │  │
│  │  │ProcessRunner │  │  FileMirror  │  │ IniHandler   │      │  │
│  │  │              │  │  (Native C#) │  │              │      │  │
│  │  │ • Run extern │  │ • Parity with │  │ • Two-pass   │  │  │
│  │  │   processes  │  │   robocopy   │  │   compilation│  │  │
│  │  │              │  │              │  │              │      │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘      │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │  │
│  │  │ProjectSync   │  │Localization  │  │MissingUncook │      │  │
│  │  │              │  │Converter     │  │Copier        │      │  │
│  │  │ • .x2proj    │  │ • UTF-8↔UTF-1│  │ • Copy missing│  │  │
│  │  │   ItemGroup  │  │   conversion │  │   uncooked   │  │  │
│  │  │   regeneration│ │              │  │              │      │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘      │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                              │                                       │
│                              ▼                                       │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Configuration/                                              │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │  │
│  │  │BuildOptions  │  │ContentOptions│  │ParserSettings│      │  │
│  │  │              │  │              │  │ (DUPLICATE!) │      │  │
│  │  │ • Mod name   │  │ • Maps to    │  │ • iniRoots   │      │  │
│  │  │ • Paths      │  │   cook       │  │ • localSrc   │      │  │
│  │  │ • Flags      │  │ • Standalone │  │ • highlander │      │  │
│  │  │              │  │   packages   │  │              │      │  │
│  │  └──────────────┘  └──────────────┘  └──────────────┘      │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘

Build Flow:
BuildCommand → BuildController → [18 build steps] → BuildResult
```

### 1.4 Current Dependencies

```
┌─────────────────────────┐         ┌─────────────────────────┐
│  XCom2ConfigParser2     │         │  XCom2ModCompiler       │
│                         │         │                         │
│  Spectre.Console.Cli    │         │  Spectre.Console.Cli    │
│  (0.53.1)               │         │  (0.48.0)               │
│                         │         │                         │
│  Microsoft.Extensions   │         │  Microsoft.Extensions   │
│  .FileSystemGlobbing    │         │  .Logging               │
│  (8.0.0)                │         │  (8.0.0)                │
│                         │         │                         │
│  .NET 10.0              │         │  ZLogger (2.5.10)       │
│                         │         │  Kokuban (0.2.0)        │
│                         │         │  System.Text.Json       │
│                         │         │  (8.0.5)                │
│                         │         │  System.Management      │
│                         │         │  (8.0.0)                │
│                         │         │  .NET 8.0-windows       │
└─────────────────────────┘         └─────────────────────────┘

Test Dependencies:
┌─────────────────────────┐         ┌─────────────────────────┐
│  XCom2ConfigParser2     │         │  XCom2ModCompiler       │
│  .Tests                 │         │  .Tests                 │
│                         │         │                         │
│  xUnit (2.9.3)          │         │  xUnit (2.9.3)          │
│  Shouldly (4.3.0)       │         │  Moq (4.20.72)          │
│  NSubstitute (5.1.0)    │         │                         │
│  Microsoft.NET.Test.Sdk │         │  Microsoft.NET.Test.Sdk │
│  coverlet.collector     │         │  coverlet.collector     │
└─────────────────────────┘         └─────────────────────────┘
```

---

## Part 2: Proposed Architecture (To-Be)

### 2.1 Recommended: Hybrid Architecture (Option C)

```
┌─────────────────────────────────────────────────────────────────────┐
│                         X2ModCompiler Solution                       │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  XCom2ConfigParser2.Core (Class Library)                     │  │
│  │  ┌──────────────────────────────────────────────────────┐   │  │
│  │  │  Core/ + Parser/ + Validation/ + StructValidation/   │   │  │
│  │  │  Configuration/ (canonical ParserSettings)           │   │  │
│  │  │                                                       │   │  │
│  │  │  Public API:                                          │   │  │
│  │  │  • FileProcessor                                      │   │  │
│  │  │  • SyntaxValidator                                    │   │  │
│  │  │  • StructMemberValidator                              │   │  │
│  │  │  • SettingsLoader                                     │   │  │
│  │  └──────────────────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────────────┘  │
│           │                           │                              │
│           │ (project reference)       │ (project reference)          │
│           ▼                           ▼                              │
│  ┌──────────────────┐     ┌──────────────────┐                     │
│  │XCom2ConfigParser2│     │   X2ModCompiler  │                     │
│  │.Cli (Exe)        │     │   (Exe)          │                     │
│  │                  │     │                  │                     │
│  │ Standalone CLI   │     │ Build system     │                     │
│  │ for config       │     │ with integrated  │                     │
│  │ validation       │     │ config validation│                     │
│  │                  │     │                  │                     │
│  │ Commands:        │     │ Commands:        │                     │
│  │ • validate-config│     │ • build          │                     │
│  │                  │     │ • clean          │                     │
│  └──────────────────┘     └──────────────────┘                     │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  X2ModCompiler.Tests                                         │  │
│  │  ┌────────────────────┐  ┌────────────────────┐             │  │
│  │  │ Compiler Tests     │  │ Config Validation  │             │  │
│  │  │ (existing)         │  │ Tests (existing)   │             │  │
│  │  └────────────────────┘  └────────────────────┘             │  │
│  └──────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 Build Pipeline with Integrated Config Validation

```
┌─────────────────────────────────────────────────────────────────────┐
│              X2ModCompiler Build Pipeline (Enhanced)                │
│                                                                      │
│  Start                                                              │
│   │                                                                 │
│   ▼                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  0. Project Synchronization                                  │   │
│  │     • Regenerate .x2proj ItemGroups                         │   │
│  └─────────────────────────────────────────────────────────────┘   │
│   │                                                                 │
│   ▼                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  0.5 Config File Validation ← NEW INTEGRATED STEP           │   │
│  │     • Load ParserSettings                                   │   │
│  │     • Find all .ini files in iniRoots                       │   │
│  │     • For each file:                                        │   │
│  │       - Tokenize directives                                 │   │
│  │       - Validate UE3 syntax                                 │   │
│  │       - Validate struct members (optional)                  │   │
│  │     • Fail build on errors                                  │   │
│  │     • Log warnings                                          │   │
│  └─────────────────────────────────────────────────────────────┘   │
│   │                                                                 │
│   ▼                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  1. Preparation                                              │   │
│  │     • Mirror mod to staging area                            │   │
│  └─────────────────────────────────────────────────────────────┘   │
│   │                                                                 │
│   ▼                                                                 │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  2-18. Remaining Build Steps...                             │   │
│  └─────────────────────────────────────────────────────────────┘   │
│   │                                                                 │
│   ▼                                                                 │
│  End (Success/Failure)                                             │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.3 Data Flow in Unified System

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Unified Data Flow                                │
│                                                                      │
│  Mod Author                                                         │
│     │                                                               │
│     │ Writes .uc and .ini files                                    │
│     ▼                                                               │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │  X2ModCompiler build command                                │   │
│  └─────────────────────────────────────────────────────────────┘   │
│     │                                                               │
│     │                                                               │
│     ├──────────────────────────────────────────┐                   │
│     │                                          │                   │
│     ▼                                          ▼                   │
│  ┌─────────────────────────┐      ┌─────────────────────────┐     │
│  │  Config Validation      │      │  Script Compilation     │     │
│  │  (XCom2ConfigParser2    │      │  (Unreal Script         │     │
│  │   Core library)         │      │   Compiler)             │     │
│  │                         │      │                         │     │
│  │  .ini files             │      │  .uc files              │     │
│  │     ↓                   │      │     ↓                   │     │
│  │  FileProcessor          │      │  ScriptCompiler         │     │
│  │     ↓                   │      │     ↓                   │     │
│  │  Diagnostics            │      │  .u packages            │     │
│  └─────────────────────────┘      └─────────────────────────┘     │
│     │                                          │                   │
│     │                                          │                   │
│     └──────────────────┬───────────────────────┘                   │
│                        │                                           │
│                        ▼                                           │
│              ┌───────────────────┐                                │
│              │  BuildController  │                                │
│              │  (orchestrates    │                                │
│              │   both pipelines) │                                │
│              └───────────────────┘                                │
│                        │                                           │
│                        ▼                                           │
│              ┌───────────────────┐                                │
│              │  Asset Cooking    │                                │
│              └───────────────────┘                                │
│                        │                                           │
│                        ▼                                           │
│              ┌───────────────────┐                                │
│              │  Final Mod        │                                │
│              │  (ready for       │                                │
│              │   deployment)     │                                │
│              └───────────────────┘                                │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.4 Alternative: Full Merger Architecture (Not Recommended)

```
┌─────────────────────────────────────────────────────────────────────┐
│                    X2ModCompiler (Merged)                           │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Program.cs                                                  │  │
│  │  Commands: build, clean, validate-config                     │  │
│  └──────────────────────────────────────────────────────────────┘  │
│           │                                                         │
│           ▼                                                         │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Application/                                                │  │
│  │  • BuildController                                           │  │
│  └──────────────────────────────────────────────────────────────┘  │
│           │                                                         │
│           ▼                                                         │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  ConfigValidation/ (merged from XCom2ConfigParser2)          │  │
│  │  • Core/                                                     │  │
│  │  • Parser/                                                   │  │
│  │  • Validation/                                               │  │
│  │  • StructValidation/                                         │  │
│  └──────────────────────────────────────────────────────────────┘  │
│           │                                                         │
│           ▼                                                         │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Compilation/ + Cooking/ + Tracking/ + Utilities/            │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                      │
│  Pros: Single project, single executable                            │
│  Cons: Large codebase (74+ files), harder to maintain               │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Part 3: Component Details

### 3.1 FileProcessor Integration

**Current (Standalone):**
```csharp
// XCom2ConfigParser2/CLI/FileProcessor.cs
public sealed class FileProcessor
{
    public FileProcessor(
        IValidator syntaxValidator,
        Configuration.ParserSettings settings,
        bool structValidationEnabled,
        ModSrcPathCache? modSrcCache = null)
    {
        // ...
    }
    
    public FileProcessingResult ProcessFile(string filePath)
    {
        // Validation logic
    }
}
```

**Proposed (With Logging):**
```csharp
// XCom2ConfigParser2.Core/CLI/FileProcessor.cs
public sealed class FileProcessor
{
    private readonly ILogger<FileProcessor>? _logger;
    
    public FileProcessor(
        IValidator syntaxValidator,
        Configuration.ParserSettings settings,
        bool structValidationEnabled,
        ModSrcPathCache? modSrcCache = null,
        ILogger<FileProcessor>? logger = null)  // NEW
    {
        _logger = logger;
        // ...
    }
    
    public FileProcessingResult ProcessFile(string filePath)
    {
        _logger?.LogInformation("Processing file: {FilePath}", filePath);
        // Validation logic
    }
}
```

### 3.2 BuildController Integration Point

**Location:** `BuildController.InvokeBuildAsync()`

```csharp
// After step 1 (Preparation), before step 2 (Src copy)
await PerformStepAsync(
    async () =>
    {
        _logger.ZLogInformation("Validating config files...");
        
        var settingsLoader = new SettingsLoader(_options.ProjectRoot);
        var settings = settingsLoader.Load();
        
        var fileProcessor = new FileProcessor(
            new SyntaxValidator(),
            settings,
            structValidationEnabled: !_options.SkipConfigValidation,
            logger: _loggerFactory.CreateLogger<FileProcessor>());
        
        var configFiles = FindConfigFiles(settings.IniRoots);
        var hasErrors = false;
        
        foreach (var file in configFiles)
        {
            var result = fileProcessor.ProcessFile(file);
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    _logger.ZLogError(
                        "Config error in {File}: {Code} - {Message}",
                        file, diagnostic.Code, diagnostic.Message);
                    hasErrors = true;
                }
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                {
                    _logger.ZLogWarning(
                        "Config warning in {File}: {Code} - {Message}",
                        file, diagnostic.Code, diagnostic.Message);
                }
            }
        }
        
        if (hasErrors)
        {
            throw new BuildFailureException("Config validation failed", 1);
        }
        
        _logger.ZLogInformation("Config validation completed successfully");
    },
    "Validating", "Validated", "config files", timings);
```

---

## Part 4: API Surface

### 4.1 XCom2ConfigParser2.Core Public API

**Classes to expose:**

```csharp
namespace XCom2ConfigParser2;

// Core types
public readonly struct Diagnostic { }
public enum DiagnosticSeverity { Error, Warning, Info }
public readonly struct Directive { }
public enum DirectiveType { SectionHeader, Kvp, Unknown }
public readonly struct Kvp { }
public enum KvpOperation { Set, InsertUnique, Insert, Remove, Clear }

// Validation
public interface IValidator { /* ... */ }
public sealed class SyntaxValidator : IValidator { /* ... */ }
public sealed class StructMemberValidator { /* ... */ }
public sealed class FileProcessor { /* ... */ }
public sealed class FileProcessingResult { /* ... */ }

// Configuration
public sealed class ParserSettings { /* ... */ }
public sealed class SettingsLoader { /* ... */ }

// Struct validation (advanced)
public sealed class StructCache { /* ... */ }
public sealed class VariableCache { /* ... */ }
public sealed class ModSrcPathCache { /* ... */ }
```

### 4.2 X2ModCompiler Build Options

**New options for config validation:**

```csharp
public class BuildOptions
{
    // Existing options...
    
    // NEW: Config validation options
    public bool SkipConfigValidation { get; init; } = false;
    public bool TreatConfigWarningsAsErrors { get; init; } = false;
    public bool SkipStructValidation { get; init; } = false;
}
```

---

## Part 5: Testing Architecture

### 5.1 Test Project Structure (Recommended: Keep Separate)

```
tests/
├── XCom2ConfigParser2.Core.Tests/
│   ├── Parser/
│   │   ├── StructParserTests.cs
│   │   ├── DirectiveTokenizerTests.cs
│   │   └── LineSplitterTests.cs
│   ├── Validation/
│   │   └── SyntaxValidatorTests.cs
│   └── StructValidation/
│       ├── UnrealScriptParserTests.cs
│       ├── VariableTypeResolverTests.cs
│       └── ...
│
└── X2ModCompiler.Tests/
    ├── Application/
    │   └── BuildControllerTests.cs
    ├── Compilation/
    │   └── ScriptCompilerTests.cs
    ├── Cooking/
    │   └── AssetCookerTests.cs
    └── Utilities/
        ├── IniHandlerTests.cs
        ├── ProjectSynchronizerTests.cs
        └── ...
```

### 5.2 Integration Test Example

```csharp
public class ConfigValidationIntegrationTests
{
    [Fact]
    public async Task Build_WithInvalidConfig_Fails()
    {
        // Arrange
        var tempMod = CreateTempModWithInvalidConfig();
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = tempMod.RootPath,
            SdkPath = TestSdkPath,
            GamePath = TestGamePath
        };
        
        // Act
        var controller = CreateBuildController(options);
        var result = await controller.InvokeBuildAsync();
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Config validation failed"));
    }
    
    [Fact]
    public async Task Build_WithSkipConfigValidation_Succeeds()
    {
        // Arrange
        var tempMod = CreateTempModWithInvalidConfig();
        var options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = tempMod.RootPath,
            SkipConfigValidation = true
        };
        
        // Act
        var controller = CreateBuildController(options);
        var result = await controller.InvokeBuildAsync();
        
        // Assert
        Assert.True(result.Success); // Build succeeds because config validation is skipped
    }
}
```

---

**End of Architecture Documentation**
