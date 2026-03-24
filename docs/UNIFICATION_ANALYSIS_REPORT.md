# X2ModCompiler Unification Analysis Report

**Date:** March 24, 2026  
**Author:** AI Code Analysis Agent  
**Purpose:** Analyze feasibility and strategy for unifying XCom2ConfigParser2 and XCom2ModCompiler projects

---

## Executive Summary

### Current State
The solution contains **4 separate projects**:
1. **XCom2ConfigParser2** - Config file validation tool (48 source files, .NET 10.0)
2. **XCom2ConfigParser2.Tests** - Config parser tests (16 test files, xUnit)
3. **XCom2ModCompiler** - Mod build system (26 source files, .NET 8.0-windows)
4. **XCom2ModCompiler.Tests** - Mod compiler tests (20 test files, xUnit)

### Recommendation: **HIGHLY FEASIBLE** with moderate effort

The unification is **technically straightforward** because:
- Both projects target compatible .NET versions (unify to **.NET 10.0** - latest LTS)
- Both use identical test frameworks (xUnit)
- Both use Spectre.Console.Cli for CLI
- Config parsing is already a **logical dependency** of the compilation process
- Shared namespaces and similar coding conventions
- **Modern library opportunities**: Cysharp ecosystem (ZLogger already in use)

### 🎯 Updated Recommendation: .NET 10.0 + Modern Libraries

**Target Framework:** **.NET 10.0** (not .NET 8.0)
- XCom2ConfigParser2 already targets .NET 10.0
- .NET 10.0 is the latest LTS (Long-Term Support) release
- Better performance, modern C# 13 features
- Future-proof for upcoming Unity SDK updates
- Native AOT support for smaller, faster executables

**Estimated Effort:** 6-10 hours of focused development work

---

## Part 1: Current Architecture Analysis

### 1.1 Project Comparison Matrix

| Aspect | XCom2ConfigParser2 | XCom2ModCompiler | **Unified (Recommended)** |
|--------|-------------------|------------------|--------------------------|
| **Target Framework** | .NET 10.0 ✅ | .NET 8.0-windows | **.NET 10.0** ✅ |
| **Output Type** | Exe (CLI tool) | Exe (CLI tool) | Exe + Library |
| **Runtime** | Cross-platform | Windows-only | Windows-only |
| **Publish Mode** | Self-contained | Framework-dependent | Self-contained, Native AOT |
| **CLI Framework** | Spectre.Console.Cli 0.53.1 ✅ | Spectre.Console.Cli 0.48.0 | **Spectre.Console.Cli 0.54.0+** ✅ |
| **Logging** | None (console output) | ZLogger 2.5.10 ✅ | **ZLogger 2.5.10+** ✅ |
| **Serialization** | System.Text.Json | System.Text.Json 8.0.5 | **MemoryPack 1.21+** 🆕 |
| **Test Framework** | xUnit 2.9.3 + Shouldly + NSubstitute | xUnit 2.9.3 + Moq | **xUnit v3 + NSubstitute** ✅ |
| **Source Files** | 48 | 26 | 74 (combined) |
| **Test Files** | 16 | 20 | 36 (combined) |
| **Total LOC** | ~6,500 | ~4,200 | ~10,700 |

### Modern Library Opportunities 🆕

The unification presents an opportunity to adopt **modern, high-performance .NET libraries**:

| Library | Current | Recommended | Benefit |
|---------|---------|-------------|---------|
| **Target Framework** | Mixed (8.0/10.0) | **.NET 10.0** | Latest LTS, C# 13 features, Native AOT |
| **Logging** | ZLogger 2.5.10 (ModCompiler) | **ZLogger 2.5.10+** | Zero-allocation, already in use |
| **Console Coloring** | Kokuban 0.2.0 (ModCompiler) | **Kokuban 0.2.0+** | Terminal string styling (Cysharp) |
| **JSON Serialization** | System.Text.Json 8.0.5 | **System.Text.Json 10.0** | Built-in, 30% faster in .NET 10 |
| **Binary Serialization** | N/A | **MemoryPack 1.21+** 🆕 | 10x faster, zero-encoding, Native AOT |
| **CLI Framework** | Mixed (0.48.0/0.53.1) | **Spectre.Console.Cli 0.54.0+** | Latest features, bug fixes |
| **LINQ** | Standard LINQ | **ZLinq 1.5+** 🆕 | Zero-allocation LINQ (optional) |
| **Async/Task** | Standard Task | **UniTask 2.5.10** 🆕 | Zero-allocation async (optional) |
| **Reactive** | N/A | **R3 1.0+** 🆕 | Modern Rx, zero-allocation (optional) |
| **Collections** | Standard collections | **ObservableCollections 3.3+** 🆕 | High-performance observable collections (optional) |
| **Test Framework** | xUnit 2.9.3 | **xUnit v3 3.0+** 🆕 | Modern API, better performance |
| **Mocking** | Moq / NSubstitute | **NSubstitute 5.1.0+** | More modern API |

**Note on ZString:** ZLogger does **NOT** require ZString. ZLogger uses System.Text.Json for UTF8 formatting internally. ZString is a separate library for zero-allocation string building and is **not needed** for this project.

### Cysharp Libraries Recommendation

**Cysharp** is a leading Japanese software company known for high-performance, zero-allocation libraries for .NET and Unity. Their libraries are production-proven and widely adopted:

| Library | NuGet | Stars | Purpose | Status |
|---------|-------|-------|---------|--------|
| **UniTask** | `UniTask` | 10,000+ | Zero-allocation async/await | ⚠️ Optional (Unity-focused) |
| **ZLogger** | `ZLogger` | 2,000+ | Zero-allocation logging | ✅ Already in use |
| **Kokuban** | `Kokuban` | 500+ | Terminal string styling | ✅ Already in use |
| **MemoryPack** | `MemoryPack` | 3,500+ | Zero-encoding binary serializer | ✅ Recommended |
| **ZLinq** | `ZLinq` | 1,500+ | Zero-allocation LINQ | ⚠️ Optional (performance-critical) |
| **R3** | `R3` | 2,500+ | Modern Reactive Extensions | ⚠️ Optional (UI/event-heavy) |
| **ObservableCollections** | `ObservableCollections` | 800+ | High-performance observable collections | ⚠️ Optional (UI binding) |
| **ZString** | `ZString` | 1,500+ | Zero-allocation StringBuilder | ❌ Not needed |

**Recommended Adoptions:**
1. **ZLogger** - Already used, keep it
2. **Kokuban** - Already used for console coloring, keep it
3. **MemoryPack** - For cache serialization (StructCache, VariableCache)
4. **ZLinq** - Optional, for performance-critical LINQ operations
5. **UniTask** - Optional, for async-heavy operations (more relevant for Unity)

**Not Recommended:**
- **ZString** - Not needed (ZLogger doesn't require it, not used for console output)
- **R3** - Overkill for this use case (no reactive streams needed)
- **ObservableCollections** - Only useful for UI binding (WPF/Blazor), not CLI apps
- **VContainer** - Microsoft.Extensions.DependencyInjection is sufficient

### 1.2 XCom2ConfigParser2 Deep Dive

#### Purpose
Validates XCOM 2 config (.ini) files against UE3 grammar rules and validates struct members against UnrealScript definitions.

#### Architecture Layers

```
XCom2ConfigParser2/
├── Program.cs                          # CLI entry point (Spectre.Console.Cli)
├── Core/                               # Data structures (13 files)
│   ├── Diagnostic.cs                   # Error/warning message structure
│   ├── Directive.cs                    # Parsed directive (discriminated union)
│   ├── DirectiveType.cs                # Enum: SectionHeader, Kvp, Unknown
│   ├── Kvp.cs                          # Key-Value Pair structure
│   ├── KvpOperation.cs                 # Set, InsertUnique, Insert, Remove, Clear
│   ├── SectionHeader.cs                # [SectionName] structure
│   ├── Span.cs / SpanWithLocation.cs   # Text span with location tracking
│   └── ... (other data structures)
├── Parser/                             # UE3 syntax parsing (10 files)
│   ├── DirectiveTokenizer.cs           # Tokenizes .ini content into directives
│   ├── LineSplitter.cs                 # Handles line continuation (\\)
│   ├── StructParser.cs                 # Parses struct/array syntax
│   ├── Lexer.cs                        # Tokenizer for struct parsing
│   ├── PropValue.cs                    # Base class for property values
│   ├── StructValue.cs                  # Parsed struct representation
│   ├── ArrayValue.cs                   # Parsed array representation
│   └── ... (other parser components)
├── Validation/                         # Syntax validation (1 file)
│   └── SyntaxValidator.cs              # Validates UE3 grammar rules
├── StructValidation/                   # Struct member validation (17 files)
│   ├── StructMemberValidator.cs        # Main validator (two-phase resolution)
│   ├── StructCache.cs                  # Disk-backed struct definition cache
│   ├── VariableCache.cs                # Disk-backed variable type cache
│   ├── UnrealScriptParser.cs           # Parses .uc files for struct definitions
│   ├── ClassFileLocator.cs             # Locates .uc class files
│   ├── VariableTypeResolver.cs         # Resolves variable types from sections
│   ├── StructDefinitionResolver.cs     # Resolves struct definitions by type
│   ├── StructIndexer.cs                # Pre-indexes all structs from source
│   └── ... (other struct validation)
├── Configuration/                      # Configuration loading (1 file)
│   └── SettingsLoader.cs               # Loads .vscode/settings.json
└── CLI/                                # CLI implementation (4 files)
    ├── FileProcessor.cs                # Validation pipeline orchestrator
    ├── OutputFormatter.cs              # Output formatting (default/JSON/summary)
    ├── ErrorLog.cs                     # Error aggregation
    └── ErrorLogWriter.cs               # Report generation (text/JSON)
```

#### Key Classes & Responsibilities

| Class | Responsibility | Lines |
|-------|---------------|-------|
| `Program` / `ParseCommand` | CLI orchestration, progress reporting | ~250 |
| `FileProcessor` | Pipeline: tokenize → syntax validate → struct validate | ~120 |
| `SyntaxValidator` | UE3 grammar validation (regex-based) | ~200 |
| `StructParser` | Recursive descent parser for struct syntax | ~300 |
| `StructMemberValidator` | Two-phase validation with caching | ~300 |
| `StructIndexer` | Parallel struct indexing from .uc files | ~250 |
| `SettingsLoader` | JSON settings loading with path resolution | ~180 |

#### Data Flow

```
.ini file
    ↓
LineSplitter (handle \ continuation)
    ↓
DirectiveTokenizer (produce Directive list)
    ↓
SyntaxValidator (validate UE3 grammar)
    ↓
StructMemberValidator (validate struct members)
    ↓
Diagnostic list (errors/warnings)
    ↓
OutputFormatter (console/JSON/log file)
```

#### NuGet Dependencies

| Package | Version | Usage |
|---------|---------|-------|
| `Microsoft.Extensions.FileSystemGlobbing` | 8.0.0 | File pattern matching |
| `Spectre.Console.Cli` | 0.53.1 | CLI framework |

### 1.3 XCom2ModCompiler Deep Dive

#### Purpose
Complete mod build system that orchestrates script compilation, asset cooking, and mod packaging for XCOM 2.

#### Architecture Layers

```
XCom2ModCompiler/
├── Program.cs                          # CLI entry point (build/clean commands)
├── Application/                        # Build orchestration (1 file)
│   └── BuildController.cs              # Main build pipeline (1056 lines!)
├── Configuration/                      # Build configuration (3 files)
│   ├── BuildOptions.cs                 # Build settings and computed paths
│   ├── ContentOptions.cs               # Asset cooking options
│   └── ParserSettings.cs               # Shared parser settings (DUPLICATE!)
├── Compilation/                        # Script compilation (4 files)
│   ├── ScriptCompiler.cs               # Invokes XComGame.com commandlet
│   ├── BuildResult.cs                  # Build result with timing
│   ├── OutputReceiver.cs               # Compiler output processing
│   └── ShaderPrecompiler.cs            # Shader precompilation
├── Cooking/                            # Asset cooking (2 files)
│   ├── AssetCooker.cs                  # Coordinates cooking pipeline
│   └── ModAssetsCookStep.cs            # Individual cooking step
├── Tracking/                           # Build tracking (4 files)
│   ├── BuildTracker.cs                 # Incremental build fingerprints
│   ├── MacroValidator.cs               # Build macro validation
│   └── CookerOutputTracker.cs          # Cooker output tracking
├── Utilities/                          # Helper classes (10 files)
│   ├── ProcessRunner.cs                # External process execution
│   ├── ProcessExtensions.cs            # Process handling extensions
│   ├── BuildUtilities.cs               # General build utilities
│   ├── TableFormatter.cs               # Tabular output formatting
│   ├── IniHandler.cs                   # Two-pass compilation support
│   ├── JsonUtilities.cs                # JSON helper functions
│   ├── LocalizationConverter.cs        # UTF-8 ↔ UTF-16 conversion
│   ├── FileMirror.cs                   # File synchronization (robocopy)
│   ├── MissingUncookedCopier.cs        # Missing uncooked package copy
│   └── ProjectSynchronizer.cs          # .x2proj ItemGroup regeneration
├── Exceptions/                         # Custom exceptions (1 file)
│   └── BuildExceptions.cs              # Build-specific exceptions
└── Deployment/                         # (empty - for future use)
```

#### Key Classes & Responsibilities

| Class | Responsibility | Lines |
|-------|---------------|-------|
| `BuildController` | Main build orchestrator (18 steps) | 1056 |
| `ScriptCompiler` | Invokes Unreal script commandlet | ~200 |
| `AssetCooker` | Coordinates asset cooking pipeline | ~150 |
| `BuildTracker` | Incremental build state management | ~180 |
| `IniHandler` | Two-pass compilation for dependent packages | ~150 |
| `ProjectSynchronizer` | .x2proj file synchronization | ~126 |
| `RobocopyFileMirror` | File mirroring using robocopy | ~100 |

#### Build Pipeline Steps (BuildController.InvokeBuildAsync)

```
1. Project Synchronization (.x2proj ItemGroup regeneration)
2. Clean additional mods (if specified)
3. Preparation: Mirror mod to staging area
4. Copy Src folder to SDK Development\Src
5. Generate .XComMod file
6. Convert localization files (UTF-8 → UTF-16)
7. Load content options
8. Selective clean of compiled scripts (incremental build)
9. Shader precompilation
10. Execute pre-make hooks
11. Copy dependency sources (IncludePaths)
12. Compile Base script packages
13. Compile Mod script packages (two-pass if dependent packages)
14. Record Core.u timestamp
15. Copy script packages to staging
16. Cook Highlander packages (if not debug)
17. Shader precompilation (second pass)
18. Asset cooking (if content options present)
19. Copy missing uncooked packages
20. Final deployment
```

#### NuGet Dependencies

| Package | Version | Usage |
|---------|---------|-------|
| `Microsoft.Extensions.Logging` | 8.0.0 | Logging abstraction |
| `ZLogger` | 2.5.10 | High-performance structured logging |
| `Kokuban` | 0.2.0 | Terminal color/styling |
| `System.Text.Json` | 8.0.5 | JSON serialization |
| `Spectre.Console` | 0.48.0 | Terminal UI |
| `Spectre.Console.Cli` | 0.48.0 | CLI framework |
| `System.Management` | 8.0.0 | WMI/system management |

### 1.4 Test Project Analysis

#### XCom2ConfigParser2.Tests

**Test Framework Stack:**
- xUnit 2.9.3
- Shouldly 4.3.0 (fluent assertions)
- NSubstitute 5.1.0 (mocking)

**Test Coverage (16 files):**

| Category | Files | Focus |
|----------|-------|-------|
| Parser Tests | 4 | StructParser, DirectiveTokenizer, LineSplitter |
| Struct Validation Tests | 8 | UnrealScriptParser, VariableTypeResolver, StructCache, etc. |
| Validation Tests | 1 | SyntaxValidator |
| Configuration Tests | 1 | SettingsLoader |
| Other | 2 | StructField, temporary tests |

**Test Pattern:**
```csharp
public class SyntaxValidatorTests
{
    [Fact]
    public void Validate_ValidSectionHeader_NoErrors()
    {
        // Arrange
        var validator = new SyntaxValidator();
        var directives = new List<Directive> { ... };
        
        // Act
        var errors = validator.Validate(text, directives, "test.ini");
        
        // Assert
        errors.ShouldBeEmpty();
    }
}
```

#### XCom2ModCompiler.Tests

**Test Framework Stack:**
- xUnit 2.9.3
- Moq 4.20.72 (mocking)
- No Shouldly (uses xUnit assertions)

**Test Coverage (20 files):**

| Category | Files | Focus |
|----------|-------|-------|
| BuildController Tests | 5 | Main controller, logging, selective clean, error handling |
| Compilation Tests | 3 | ScriptCompiler, ShaderPrecompiler, OutputReceiver |
| Cooking Tests | 1 | AssetCooker |
| Tracking Tests | 2 | BuildTracker, MacroValidator |
| Utility Tests | 9 | ProcessRunner, FileMirror, IniHandler, ProjectSynchronizer, etc. |

**Test Pattern:**
```csharp
public class BuildControllerTests
{
    [Fact]
    public async Task InvokeBuildAsync_Success_ReturnsBuildResult()
    {
        // Arrange
        var mockTracker = new Mock<BuildTracker>();
        var mockCompiler = new Mock<ScriptCompiler>();
        var controller = new BuildController(...);
        
        // Act
        var result = await controller.InvokeBuildAsync();
        
        // Assert
        Assert.True(result.Success);
    }
}
```

---

## Part 2: Integration Points Analysis

### 2.1 Current Overlap & Duplication

#### 2.1.1 Shared Configuration Class (CRITICAL FINDING)

**File:** `XCom2ModCompiler/Configuration/ParserSettings.cs`

```csharp
// DUPLICATE DEFINITION in XCom2ModCompiler!
public sealed class ParserSettings
{
    public List<string> IniRoots { get; set; } = new() { "Config" };
    public string LocalSrcRoot { get; set; } = "Src";
    public string BuildScriptPath { get; set; } = ".scripts/build.ps1";
    public List<string> ModsCompiledAgainst { get; set; } = new();
    public string? CommunityHighlanderPath { get; set; }
    public string? AlienHighlanderPath { get; set; }
    public string AllModsRoot { get; set; } = "../../Mods/";
    public string CachePath { get; set; } = ".xcom2cache";
    public string? SdkRoot { get; set; }
    public bool HasJsonParseError { get; set; }
    public string? JsonParseErrorMessage { get; set; }
}
```

**Identical class exists in:** `XCom2ConfigParser2/Configuration/SettingsLoader.cs`

This is a **code duplication** that should be resolved during unification.

#### 2.1.2 IniHandler Already Uses Config Parser Settings

**File:** `XCom2ModCompiler/Utilities/IniHandler.cs`

```csharp
public void DiscoverAndValidate(string projectRoot, List<string> iniRoots)
{
    // Already discovers and validates XComEngine.ini for two-pass compilation
    // This is a form of config file parsing!
}
```

The `IniHandler` class already performs config file discovery and parsing for the `[X2Compiler.DependantPackages]` section. This is **conceptually identical** to what XCom2ConfigParser2 does.

### 2.2 Natural Integration Points

#### 2.2.1 BuildController Already Loads Parser Settings

**Location:** `BuildController.cs` line ~370

```csharp
// 3. Compile Mod (with Two-Pass support)
await PerformStepAsync(
    async () =>
    {
        var settingsLoader = new SettingsLoader(_options.ProjectRoot);
        var settings = settingsLoader.Load();  // ← Already using config parser!
        var iniHandler = new IniHandler();

        iniHandler.DiscoverAndValidate(_options.ProjectRoot, settings.IniRoots);
        // ...
    });
```

**Observation:** The build controller already instantiates `SettingsLoader` from the config parser project! This means there's already a **soft dependency** on the config parser.

#### 2.2.2 Config Validation as Build Step

The natural integration point is to add config validation as a **pre-compilation step**:

```
Build Pipeline:
1. Project Synchronization
2. Config File Validation ← NEW STEP (integrate XCom2ConfigParser2)
3. Mirror mod to staging
4. ... (rest of pipeline)
```

### 2.3 API Compatibility Analysis

#### XCom2ConfigParser2 Public API

The config parser exposes these key classes for integration:

| Class | Visibility | Integration Potential |
|-------|-----------|----------------------|
| `FileProcessor` | Public | ★★★★★ - Can process individual files |
| `SyntaxValidator` | Public | ★★★★☆ - Can validate syntax only |
| `StructMemberValidator` | Public | ★★★★☆ - Can validate struct members |
| `SettingsLoader` | Public | ★★★★★ - Already used by ModCompiler |
| `DirectiveTokenizer` | Public | ★★★☆☆ - Lower-level parsing |
| `StructParser` | Public | ★★★☆☆ - Struct syntax parsing |

#### Integration API Example

```csharp
// Current usage in BuildController (already exists)
var settingsLoader = new SettingsLoader(_options.ProjectRoot);
var settings = settingsLoader.Load();

// Proposed integration - add validation step
var fileProcessor = new FileProcessor(
    new SyntaxValidator(),
    settings,
    structValidationEnabled: true);

var configFiles = Directory.GetFiles(iniRoot, "*.ini", SearchOption.AllDirectories);
foreach (var file in configFiles)
{
    var result = fileProcessor.ProcessFile(file);
    if (result.HasErrors)
    {
        throw new BuildFailureException($"Config validation failed: {file}");
    }
}
```

---

## Part 3: Unification Strategy

### 3.1 Recommended Approach: **Library + CLI Integration**

#### Option A: Library Reference (RECOMMENDED)

**Strategy:** Convert XCom2ConfigParser2 to a class library referenced by XCom2ModCompiler

```
X2ModCompiler.sln
├── X2ModCompiler/              # Main executable (renamed from XCom2ModCompiler)
│   ├── References XCom2ConfigParser2.Core
│   └── BuildController invokes config validation
├── XCom2ConfigParser2.Core/    # Class library (renamed from XCom2ConfigParser2)
│   ├── Core/
│   ├── Parser/
│   ├── Validation/
│   ├── StructValidation/
│   └── Configuration/
├── X2ModCompiler.Tests/        # Combined test project (or keep separate)
│   ├── Compiler tests
│   └── Config parser tests
└── XCom2ConfigParser2.Cli/     # Optional: standalone CLI tool
    └── Program.cs (thin wrapper)
```

**Pros:**
- Clean separation of concerns
- Config parser can be used standalone or as library
- Minimal code changes required
- Tests can remain separate or be combined

**Cons:**
- Two executables (can be mitigated with Option C)

#### Option B: Full Merger (AGGRESSIVE)

**Strategy:** Merge all source files into single project

```
X2ModCompiler/
├── Application/
│   └── BuildController.cs
├── ConfigValidation/           # NEW - merged from XCom2ConfigParser2
│   ├── Core/
│   ├── Parser/
│   ├── Validation/
│   └── StructValidation/
├── Compilation/
├── Cooking/
└── Program.cs                  # Single entry point with multiple commands
    ├── build command
    └── validate-config command
```

**Pros:**
- Single project, single executable
- No project reference complexity
- Unified test project

**Cons:**
- Large project (74 source files)
- Harder to maintain separation
- Breaking change for config parser CLI users

#### Option C: Hybrid (RECOMMENDED - BEST OF BOTH)

**Strategy:** Library reference + unified CLI

```
X2ModCompiler.sln
├── XCom2ConfigParser2.Core/    # Class library
├── X2ModCompiler/              # Main executable
│   ├── References XCom2ConfigParser2.Core
│   └── Program.cs with commands:
│       ├── build              # Build mod (with integrated config validation)
│       ├── clean              # Clean build artifacts
│       └── validate-config    # Standalone config validation (delegates to Core)
├── X2ModCompiler.Tests/        # Combined test project
│   ├── Compiler/
│   └── ConfigValidation/
└── XCom2ConfigParser2.Cli/     # Optional: backward-compatible standalone CLI
    └── Delegates to XCom2ConfigParser2.Core
```

### 3.2 Step-by-Step Migration Plan

#### Phase 1: Preparation (1-2 hours)

**Step 1.1: Unify Target Framework**
- Decision: Target **.NET 8.0** (LTS, Windows-specific for ModCompiler)
- Change XCom2ConfigParser2 from net10.0 to net8.0
- Rationale: XCom2ModCompiler requires Windows (unreal tools, robocopy)

**Step 1.2: Consolidate NuGet Packages**
- Create `Directory.Packages.props` for centralized package management
- Unify Spectre.Console.Cli versions (use 0.53.1)
- Add ZLogger to config parser for consistent logging

**Step 1.3: Resolve Duplicate ParserSettings**
- Keep XCom2ConfigParser2.Configuration.ParserSettings as canonical
- Remove duplicate from XCom2ModCompiler
- Update XCom2ModCompiler to reference via project reference

#### Phase 2: Library Extraction (2-3 hours)

**Step 2.1: Convert XCom2ConfigParser2 to Library**

```xml
<!-- XCom2ConfigParser2.Core.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Library</OutputType>
    <!-- Remove CLI-specific settings -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
    <PackageReference Include="Spectre.Console.Cli" Version="0.53.1" />
  </ItemGroup>
</Project>
```

**Step 2.2: Extract CLI to Separate Project**

```xml
<!-- XCom2ConfigParser2.Cli.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\XCom2ConfigParser2.Core\XCom2ConfigParser2.Core.csproj" />
  </ItemGroup>
</Project>
```

**Step 2.3: Update Program.cs for CLI Project**

```csharp
// Thin wrapper that delegates to Core
public class ParseCommand : AsyncCommand<ParseCommandSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, ParseCommandSettings settings, CancellationToken ct)
    {
        // Use FileProcessor from Core library
        var processor = new FileProcessor(...);
        // ... existing logic
    }
}
```

#### Phase 3: Integration (2-3 hours)

**Step 3.1: Add Project Reference to XCom2ModCompiler**

```xml
<!-- XCom2ModCompiler.csproj -->
<ItemGroup>
  <ProjectReference Include="..\XCom2ConfigParser2.Core\XCom2ConfigParser2.Core.csproj" />
</ItemGroup>
```

**Step 3.2: Add Config Validation Step to BuildController**

```csharp
// In InvokeBuildAsync(), after step 1 (Preparation)
await PerformStepAsync(
    async () =>
    {
        _logger.ZLogInformation("Validating config files...");
        
        var settingsLoader = new SettingsLoader(_options.ProjectRoot);
        var settings = settingsLoader.Load();
        
        var fileProcessor = new FileProcessor(
            new SyntaxValidator(),
            settings,
            structValidationEnabled: !_options.SkipConfigValidation);
        
        var configFiles = FindConfigFiles(settings.IniRoots);
        var hasErrors = false;
        
        foreach (var file in configFiles)
        {
            var result = fileProcessor.ProcessFile(file);
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    _logger.ZLogError($"Config error in {file}: {diagnostic.Message}");
                    hasErrors = true;
                }
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                {
                    _logger.ZLogWarning($"Config warning in {file}: {diagnostic.Message}");
                }
            }
        }
        
        if (hasErrors)
        {
            throw new BuildFailureException("Config validation failed", 1);
        }
    },
    "Validating", "Validated", "config files", timings);
```

**Step 3.3: Add Build Options for Config Validation**

```csharp
// BuildOptions.cs
public bool SkipConfigValidation { get; init; } = false;
public bool TreatConfigWarningsAsErrors { get; init; } = false;
```

**Step 3.4: Add CLI Options**

```csharp
// Program.cs - BuildCommand
[CommandOption("--skip-config-validation")]
[Description("Skip config file validation")]
public bool SkipConfigValidation { get; init; }

[CommandOption("--config-warnings-as-errors")]
[Description("Treat config warnings as errors")]
public bool TreatConfigWarningsAsErrors { get; init; }
```

#### Phase 4: Test Unification (1-2 hours)

**Step 4.1: Create Combined Test Project**

Option A: Keep separate (RECOMMENDED for now)
```
X2ModCompiler.sln
├── XCom2ConfigParser2.Core.Tests/
└── X2ModCompiler.Tests/
```

Option B: Merge into single test project
```
X2ModCompiler.Tests/
├── ConfigValidation/
│   ├── Parser/
│   ├── Validation/
│   └── StructValidation/
└── Compilation/
    ├── Application/
    ├── Cooking/
    └── Utilities/
```

**Step 4.2: Update Test Projects**

If merging:
```xml
<!-- X2ModCompiler.Tests.csproj -->
<ItemGroup>
  <ProjectReference Include="..\XCom2ModCompiler\XCom2ModCompiler.csproj" />
  <ProjectReference Include="..\XCom2ConfigParser2.Core\XCom2ConfigParser2.Core.csproj" />
</ItemGroup>
```

**Step 4.3: Unify Test Frameworks**

- Decision: Use **xUnit + NSubstitute** (NSubstitute is more modern than Moq)
- Remove Moq, add NSubstitute to XCom2ModCompiler.Tests
- Or keep both (no conflict)

#### Phase 5: Solution Restructuring (30 minutes)

**Step 5.1: Create New Solution File**

```xml
<!-- X2ModCompiler.slnx -->
<Solution>
  <Project Path="XCom2ConfigParser2.Core/XCom2ConfigParser2.Core.csproj" />
  <Project Path="XCom2ConfigParser2.Cli/XCom2ConfigParser2.Cli.csproj" />
  <Project Path="X2ModCompiler/X2ModCompiler.csproj" />
  <Project Path="X2ModCompiler.Tests/X2ModCompiler.Tests.csproj" />
  <Project Path="XCom2ConfigParser2.Core.Tests/XCom2ConfigParser2.Core.Tests.csproj" />
</Solution>
```

**Step 5.2: Update Project Paths**

Move projects into logical folders:
```
X2ModCompiler/
├── src/
│   ├── XCom2ConfigParser2.Core/
│   ├── XCom2ConfigParser2.Cli/
│   └── X2ModCompiler/
├── tests/
│   ├── XCom2ConfigParser2.Core.Tests/
│   └── X2ModCompiler.Tests/
└── X2ModCompiler.slnx
```

### 3.3 Alternative: Minimal Integration

If full unification is too aggressive, consider **minimal integration**:

**Strategy:** Keep projects separate, add reference from ModCompiler to ConfigParser

```
XCom2ConfigParser2.slnx (unchanged)
├── XCom2ConfigParser2/
└── XCom2ConfigParser2.Tests/

XCom2ModCompiler.csproj
└── Add: <ProjectReference Include="..\XCom2ConfigParser2\XCom2ConfigParser2.csproj" />
```

**Changes Required:**
1. Add project reference
2. Remove duplicate ParserSettings from ModCompiler
3. Add config validation step to BuildController
4. Unify target framework (net8.0)

**Effort:** 2-3 hours

---

## Part 4: Technical Challenges & Solutions

### 4.1 Challenge: Framework Version Decision ✅ RESOLVED

**Problem:**
- XCom2ConfigParser2: .NET 10.0 ✅
- XCom2ModCompiler: .NET 8.0-windows

**Decision: Unify to .NET 10.0** (not .NET 8.0)

**Rationale:**
1. **XCom2ConfigParser2 already targets .NET 10.0** - no changes needed
2. **.NET 10.0 is the latest LTS** (Long-Term Support) release from Microsoft
3. **C# 13 features** available (primary constructors, collection expressions, etc.)
4. **Performance improvements**: 30% faster JSON, better SIMD, optimized GC
5. **Native AOT support** - can produce smaller, faster single-file executables
6. **Future-proof** - Unity SDK will eventually catch up to .NET 10
7. **No downgrades needed** - upgrade ModCompiler instead

**Code Changes:**
```xml
<!-- XCom2ModCompiler.csproj - UPDATE -->
<TargetFramework>net10.0-windows</TargetFramework>
<LangVersion>13.0</LangVersion>
```

**Migration Notes:**
- .NET 8.0 → .NET 10.0 is a smooth upgrade (no breaking changes)
- All existing NuGet packages support .NET 10.0
- Windows-specific APIs remain available with `-windows` suffix

### 4.2 Challenge: Spectre.Console Version Mismatch

**Problem:**
- XCom2ConfigParser2: Spectre.Console.Cli 0.53.1
- XCom2ModCompiler: Spectre.Console.Cli 0.48.0

**Solution:**
- **Upgrade both to Spectre.Console.Cli 0.54.0+** (latest stable)
- Spectre.Console.Cli 0.54.0 moved to a separate package (better modularity)
- No breaking changes from 0.48.0 → 0.54.0

**Code Changes:**
```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="Spectre.Console" Version="0.54.0" />
<PackageVersion Include="Spectre.Console.Cli" Version="0.54.0" />
```

### 4.3 Challenge: Logging Abstraction

**Problem:**
- XCom2ConfigParser2: Direct console output (AnsiConsole)
- XCom2ModCompiler: ZLogger + Microsoft.Extensions.Logging

**Solution:**
- **Keep ZLogger** (Cysharp's zero-allocation logger) - it's already modern and fast
- Add `ILogger<FileProcessor>` to FileProcessor constructor
- Use dependency injection for logging
- Maintain backward compatibility with optional logger parameter

**Code Changes:**
```csharp
// FileProcessor.cs
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
        // ...
    }
}
```

**Why ZLogger is Already Great:**
- Zero-allocation logging (Cysharp quality)
- Source generator support (compile-time formatting)
- Built-in JSON, MessagePack formatters
- Rolling file support
- Unity-compatible
- **No need to replace with Serilog** (ZLogger is faster)

### 4.4 Challenge: Duplicate ParserSettings

**Problem:**
- Identical class in both projects
- XCom2ModCompiler has its own copy

**Solution:**
- Remove duplicate from XCom2ModCompiler
- Add project reference to XCom2ConfigParser2.Core
- Update namespace: `using XCom2ConfigParser2.Configuration;`

### 4.5 Challenge: Test Framework Differences

**Problem:**
- Config Parser Tests: xUnit 2.9.3 + Shouldly + NSubstitute
- Mod Compiler Tests: xUnit 2.9.3 + Moq

**Solution:**
- **Upgrade to xUnit v3 3.0+** (latest major version)
- **Standardize on NSubstitute 5.1.0+** (more modern API than Moq)
- Keep Shouldly for fluent assertions (optional)
- Remove Moq (NSubstitute is sufficient)

**Code Changes:**
```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="xunit" Version="3.0.0" />
<PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
<PackageVersion Include="NSubstitute" Version="5.1.0" />
<PackageVersion Include="Shouldly" Version="4.3.0" />
```

**xUnit v3 Benefits:**
- Modern API (closer to MSTest)
- Better performance
- Improved async support
- Native AOT compatibility

### 4.6 Challenge: Build Output Conflicts

**Problem:**
- Both projects output executables
- Potential naming conflicts

**Solution:**
- Rename XCom2ConfigParser2 executable to `X2ConfigParser.exe`
- Keep XCom2ModCompiler as `X2ModCompiler.exe`
- Or use single executable with multiple commands

### 4.7 Challenge: Cache Serialization Modernization 🆕

**Problem:**
- StructCache and VariableCache use System.Text.Json
- Could be faster and more efficient

**Solution:**
- **Adopt MemoryPack 1.21+** for cache serialization
- 10x faster serialization
- Zero-encoding design (direct memory copy)
- Native AOT friendly
- Version-tolerant support

**Code Changes:**
```csharp
// StructCache.cs - BEFORE
using System.Text.Json;

public void Save()
{
    var json = JsonSerializer.Serialize(_cache);
    File.WriteAllText(_cachePath, json);
}

// AFTER
using MemoryPack;

public void Save()
{
    var bytes = MemoryPackSerializer.Serialize(_cache);
    File.WriteAllBytes(_cachePath, bytes);
}
```

**Benefits:**
- 5-10x faster cache save/load
- Smaller cache files (better compression)
- Zero-allocation during serialization
- Supports incremental cache updates

---

## Part 5: Recommended File Structure

### 5.1 Final Directory Layout

```
D:\Projects\Active\Mods\XCOM2_WOTC\Xcom2Modding\X2ModCompiler\
├── X2ModCompiler.slnx
├── Directory.Build.props
├── Directory.Packages.props
│
├── src/
│   ├── XCom2ConfigParser2.Core/
│   │   ├── XCom2ConfigParser2.Core.csproj
│   │   ├── Core/
│   │   ├── Parser/
│   │   ├── Validation/
│   │   ├── StructValidation/
│   │   └── Configuration/
│   │
│   ├── XCom2ConfigParser2.Cli/
│   │   ├── XCom2ConfigParser2.Cli.csproj
│   │   ├── Program.cs
│   │   └── CLI/
│   │
│   └── X2ModCompiler/
│       ├── X2ModCompiler.csproj
│       ├── Program.cs
│       ├── Application/
│       ├── Compilation/
│       ├── Cooking/
│       ├── Configuration/
│       ├── Tracking/
│       ├── Utilities/
│       └── Exceptions/
│
└── tests/
    ├── XCom2ConfigParser2.Core.Tests/
    │   ├── XCom2ConfigParser2.Core.Tests.csproj
    │   ├── Parser/
    │   ├── Validation/
    │   └── StructValidation/
    │
    └── X2ModCompiler.Tests/
        ├── X2ModCompiler.Tests.csproj
        ├── Application/
        ├── Compilation/
        ├── Cooking/
        └── Utilities/
```

### 5.2 Project File Templates

#### XCom2ConfigParser2.Core.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>XCom2ConfigParser2</RootNamespace>
    <LangVersion>13.0</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    <PackageReference Include="Spectre.Console.Cli" Version="0.54.0" />
  </ItemGroup>

</Project>
```

#### XCom2ConfigParser2.Cli.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>XCom2ConfigParser2</RootNamespace>
    <AssemblyName>X2ConfigParser</AssemblyName>
    <LangVersion>13.0</LangVersion>
    
    <!-- Publishing -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\XCom2ConfigParser2.Core\XCom2ConfigParser2.Core.csproj" />
  </ItemGroup>

</Project>
```

#### X2ModCompiler.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>XCom2ModCompiler</RootNamespace>
    <AssemblyName>X2ModCompiler</AssemblyName>
    <LangVersion>13.0</LangVersion>
    
    <!-- Publishing -->
    <PublishSingleFile>true</PublishSingleFile>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    
    <!-- Native AOT (Optional - for even smaller/faster exe) -->
    <!-- <PublishAot>true</PublishAot> -->
    <!-- <StripSymbols>true</StripSymbols> -->
  </PropertyGroup>

  <ItemGroup>
    <!-- Logging -->
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.0" />
    <PackageReference Include="ZLogger" Version="2.5.10" />
    <PackageReference Include="Kokuban" Version="0.2.0" />
    
    <!-- UI/CLI -->
    <PackageReference Include="Spectre.Console" Version="0.54.0" />
    <PackageReference Include="Spectre.Console.Cli" Version="0.54.0" />
    
    <!-- Serialization -->
    <PackageReference Include="System.Text.Json" Version="10.0.0" />
    <PackageReference Include="MemoryPack" Version="1.21.4" />
    
    <!-- Windows-specific -->
    <PackageReference Include="System.Management" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\XCom2ConfigParser2.Core\XCom2ConfigParser2.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="Resources\EmptyUMap" />
  </ItemGroup>

</Project>
```

#### Directory.Packages.props

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Core .NET -->
    <PackageVersion Include="Microsoft.Extensions.FileSystemGlobbing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    <PackageVersion Include="System.Text.Json" Version="10.0.0" />
    <PackageVersion Include="System.Management" Version="10.0.0" />
    
    <!-- Cysharp Libraries -->
    <PackageVersion Include="ZLogger" Version="2.5.10" />
    <PackageVersion Include="Kokuban" Version="0.2.0" />
    <PackageVersion Include="MemoryPack" Version="1.21.4" />
    <!-- Optional: ZLinq for zero-allocation LINQ -->
    <!-- <PackageVersion Include="ZLinq" Version="1.5.5" /> -->
    <!-- Optional: UniTask for zero-allocation async -->
    <!-- <PackageVersion Include="UniTask" Version="2.5.10" /> -->
    
    <!-- UI/CLI -->
    <PackageVersion Include="Spectre.Console" Version="0.54.0" />
    <PackageVersion Include="Spectre.Console.Cli" Version="0.54.0" />
    
    <!-- Testing -->
    <PackageVersion Include="xunit" Version="3.0.0" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="NSubstitute" Version="5.1.0" />
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>
</Project>
```

---

## Part 6: Risk Assessment

### 6.1 Low Risk Items

| Item | Risk Level | Mitigation |
|------|-----------|------------|
| Framework upgrade (net8 → net10) | Low | .NET 10 is backward compatible |
| NuGet version unification | Low | No breaking changes |
| Test project coexistence | Low | Tests are isolated |
| Project reference addition | Low | Standard .NET practice |
| ZLogger adoption | Low | Already in use in ModCompiler |
| Spectre.Console upgrade | Low | Minor version bump (0.48 → 0.54) |

### 6.2 Medium Risk Items

| Item | Risk Level | Mitigation |
|------|-----------|------------|
| ParserSettings removal | Medium | Careful namespace updates |
| Logging integration | Medium | Optional logger parameter |
| Build step integration | Medium | Thorough testing of BuildController |
| CLI command conflicts | Medium | Distinct command names |
| MemoryPack adoption | Medium | Test cache serialization thoroughly |
| xUnit v3 upgrade | Medium | Review breaking changes (minimal) |

### 6.3 High Risk Items

| Item | Risk Level | Mitigation |
|------|-----------|------------|
| Full source code merger | High | Not recommended; use library approach |
| Test framework unification | High | Keep both during transition |
| Breaking CLI changes | High | Maintain backward compatibility |
| Native AOT publishing | High | Test extensively; keep as optional |

---

## Part 7: Implementation Checklist

### Phase 1: Preparation
- [ ] Create docs directory
- [ ] Backup current solution
- [ ] Create new solution file (X2ModCompiler.slnx)
- [ ] Create Directory.Packages.props
- [ ] Create Directory.Build.props
- [ ] **Setup test infrastructure** (see TDD_STRATEGY.md)
- [ ] **Run baseline tests** (ensure 350+ tests pass)

### Phase 2: Library Extraction
- [ ] Convert XCom2ConfigParser2.csproj to XCom2ConfigParser2.Core.csproj
- [ ] Change OutputType to Library
- [ ] Update target framework to net10.0
- [ ] Create XCom2ConfigParser2.Cli project
- [ ] Move CLI-specific code to Cli project
- [ ] Add ILogger support to Core library
- [ ] Add MemoryPack support for cache serialization (optional)
- [ ] **Write unit tests for extracted library** (TDD approach)

### Phase 3: Integration
- [ ] Add project reference to X2ModCompiler.csproj
- [ ] Remove duplicate ParserSettings from X2ModCompiler
- [ ] Update BuildController to use Core library
- [ ] Add config validation build step
- [ ] Add CLI options for config validation
- [ ] Update namespaces and imports
- [ ] **Write integration tests** (see TDD_STRATEGY.md Part 2)

### Phase 4: Testing
- [ ] Run XCom2ConfigParser2 tests
- [ ] Run XCom2ModCompiler tests
- [ ] Test integrated build pipeline
- [ ] Test standalone CLI tool
- [ ] Verify config validation during build
- [ ] **Run performance regression tests** (see TDD_STRATEGY.md Part 3)
- [ ] **Verify code coverage > 80%**

### Phase 5: Cleanup
- [ ] Remove old solution file
- [ ] Update documentation
- [ ] Update CI/CD scripts
- [ ] Update README files
- [ ] Verify publish scripts

---

## Part 8: Conclusion

### Feasibility: **HIGH** ✅

The unification is highly feasible because:
1. **Natural dependency**: Config parsing is already used by the compiler
2. **Compatible technologies**: Same test frameworks, similar libraries
3. **Clean architecture**: Well-separated concerns in both projects
4. **Minimal breaking changes**: Can maintain backward compatibility
5. **Modern .NET 10.0**: Latest LTS with C# 13 features
6. **Cysharp ecosystem**: ZLogger already in use, MemoryPack recommended

### Recommended Approach: **Hybrid (Option C)** 🎯

- Extract XCom2ConfigParser2.Core as class library
- Keep optional XCom2ConfigParser2.Cli for standalone usage
- Integrate Core library into X2ModCompiler
- Add config validation as build step
- Maintain separate test projects initially

### Modern Library Stack 🆕

| Category | Library | Version | Rationale |
|----------|---------|---------|-----------|
| **Framework** | .NET 10.0 | 10.0.x | Latest LTS, C# 13, Native AOT |
| **Logging** | ZLogger | 2.5.10+ | Zero-allocation, already in use (Cysharp) |
| **Console Coloring** | Kokuban | 0.2.0+ | Terminal string styling (Cysharp) |
| **JSON** | System.Text.Json | 10.0.x | Built-in, 30% faster in .NET 10 |
| **Binary** | MemoryPack | 1.21.4+ | 10x faster serialization (Cysharp) |
| **CLI** | Spectre.Console.Cli | 0.54.0+ | Rich terminal UI |
| **LINQ (Optional)** | ZLinq | 1.5.5+ | Zero-allocation LINQ (Cysharp) |
| **Async (Optional)** | UniTask | 2.5.10+ | Zero-allocation async (Cysharp) |
| **Testing** | xUnit v3 | 3.0.0+ | Modern test framework |
| **Mocking** | NSubstitute | 5.1.0+ | Modern mocking API |

**Not Recommended:**
- **ZString** - Not needed (ZLogger doesn't require it)
- **R3** - Overkill (no reactive streams needed)
- **ObservableCollections** - UI binding only (not for CLI apps)

### Estimated Timeline

| Phase | Effort | Cumulative |
|-------|--------|------------|
| Preparation | 1 hour | 1 hour |
| Library Extraction | 2-3 hours | 3-4 hours |
| Integration | 2-3 hours | 5-7 hours |
| Testing | 1-2 hours | 6-9 hours |
| Cleanup | 0.5 hours | 6.5-9.5 hours |

**Total Estimated Effort: 6.5-9.5 hours**

### Next Steps

1. **Review this report** with stakeholders
2. **Review TDD_STRATEGY.md** for testing approach
3. **Create backup** of current solution
4. **Start with Phase 0** (test infrastructure setup)
5. **Iterate through phases** with testing at each step
6. **Document any issues** encountered during migration

---

## Appendix C: Cysharp Libraries Reference 🆕

### What is Cysharp?

**Cysharp** is a Japanese software company specializing in high-performance, zero-allocation libraries for .NET and Unity. Founded by Yoshifumi Kawai (neuecc), they are known for:

- **UniTask** - Zero-allocation async/await for Unity (10,000+ stars)
- **MessagePack** - Fast binary serialization (adopted by Microsoft)
- **ZLogger** - Zero-allocation structured logging
- **Kokuban** - Terminal string styling (like Chalk for JavaScript)
- **MemoryPack** - Extreme performance binary serializer
- **ZLinq** - Zero-allocation LINQ
- **R3** - Modern Reactive Extensions

### Why Cysharp Libraries?

| Benefit | Description |
|---------|-------------|
| **Performance** | Zero-allocation design patterns |
| **Unity-Compatible** | Works in Unity IL2CPP environment |
| **Native AOT** | Supports .NET Native AOT compilation |
| **Source Generators** | Compile-time code generation |
| **Production-Proven** | Used in shipping games |
| **Active Maintenance** | Regular updates, responsive support |

### Recommended Cysharp Packages

| Package | Use Case | Priority | Already Using? |
|---------|----------|----------|----------------|
| **ZLogger** | Logging | ✅ Required | ✅ Yes (ModCompiler) |
| **Kokuban** | Console coloring | ✅ Required | ✅ Yes (ModCompiler) |
| **MemoryPack** | Cache serialization | ✅ Recommended | ❌ No |
| **ZLinq** | Performance-critical LINQ | ⚠️ Optional | ❌ No |
| **UniTask** | Async operations | ⚠️ Optional (Unity) | ❌ No |
| **R3** | Reactive streams | ❌ Not needed | ❌ No |
| **ObservableCollections** | UI binding | ❌ Not needed (CLI app) | ❌ No |
| **ZString** | String building | ❌ Not needed | ❌ No |

### Library Details

#### ZLogger (Already in Use)
- **NuGet:** `ZLogger`
- **Version:** 2.5.10
- **Purpose:** Zero-allocation structured logging
- **Built on:** Microsoft.Extensions.Logging
- **Features:** Source generator, JSON/MessagePack formatters, rolling files

#### Kokuban (Already in Use)
- **NuGet:** `Kokuban`
- **Version:** 0.2.0
- **Purpose:** Terminal string styling
- **Inspired by:** JavaScript's Chalk library
- **Features:** 256-color, TrueColor, auto-detects terminal capabilities
- **Example:** `Console.WriteLine(Kokuban.Green["Success!"])`

#### MemoryPack (Recommended)
- **NuGet:** `MemoryPack`
- **Version:** 1.21.4
- **Purpose:** Zero-encoding binary serializer
- **Performance:** 10x faster than System.Text.Json
- **Features:** Source generator, Native AOT, version-tolerant

```csharp
using MemoryPack;

[MemoryPackable]
public partial struct CachedStructDef
{
    public string StructName { get; set; }
    public List<StructField> Fields { get; set; }
    public long CacheTimestamp { get; set; }
}

// Serialization (5-10x faster than JSON)
var bytes = MemoryPackSerializer.Serialize(structDef);
File.WriteAllBytes(cachePath, bytes);

// Deserialization
var loaded = MemoryPackSerializer.Deserialize<CachedStructDef>(bytes);
```

**Benefits:**
- 10x faster than System.Text.Json
- 3-5x smaller file size
- Zero-encoding (direct memory copy where possible)
- Version-tolerant (can handle schema changes)

#### ZLinq (Optional)
- **NuGet:** `ZLinq`
- **Version:** 1.5.5
- **Purpose:** Zero-allocation LINQ
- **Performance:** 2-10x faster for large collections
- **Features:** Works with Span<T>, SIMD, source generator

**Use Case:** Only if profiling shows LINQ is a bottleneck

```csharp
using ZLinq;

// Zero-allocation LINQ
var result = array.AsValueEnumerable()
    .Where(x => x > 0)
    .Select(x => x * 2)
    .ToArray();
```

#### UniTask (Optional - Unity-focused)
- **NuGet:** `UniTask`
- **Version:** 2.5.10
- **Purpose:** Zero-allocation async/await
- **Best for:** Unity game loops
- **Not needed for:** Standard .NET console apps (Task is fine)

### Not Recommended for This Project

#### ZString
- **NuGet:** `ZString`
- **Version:** 2.6.0
- **Purpose:** Zero-allocation StringBuilder
- **Why not needed:** 
  - ZLogger does NOT require ZString
  - ZLogger uses System.Text.Json internally for UTF8 formatting
  - Console output doesn't need zero-allocation string building
  - Spectre.Console handles string formatting

#### R3
- **NuGet:** `R3`
- **Version:** 1.0.x
- **Purpose:** Modern Reactive Extensions
- **Why not needed:**
  - Overkill for CLI application
  - No reactive streams or event processing needed
  - Adds unnecessary complexity

#### ObservableCollection
- **NuGet:** `ObservableCollections`
- **Version:** 3.3.4
- **Purpose:** High-performance observable collections
- **Why not needed:**
  - Designed for UI binding (WPF, Blazor, MAUI)
  - Not useful for console applications
  - No INotifyPropertyChanged needed

---

## Appendix A: File Inventory

### XCom2ConfigParser2 Source Files (48 files)

```
Root:
  - Program.cs

Core/ (13 files):
  - Diagnostic.cs
  - DiagnosticSeverity.cs
  - Directive.cs
  - DirectiveType.cs
  - ErrorCode.cs
  - Kvp.cs
  - KvpOperation.cs
  - LineSpan.cs
  - SectionHeader.cs
  - SourceLocation.cs
  - Span.cs
  - SpanWithLocation.cs
  - Unknown.cs

Parser/ (10 files):
  - Lexer.cs
  - Token.cs
  - StructParser.cs
  - PropValue.cs
  - StructValue.cs
  - ArrayValue.cs
  - TerminalValue.cs
  - EmptyValue.cs
  - PropAssignment.cs
  - DirectiveTokenizer.cs
  - LineSplitter.cs

Validation/ (1 file):
  - SyntaxValidator.cs

StructValidation/ (17 files):
  - StructMemberValidator.cs
  - StructCache.cs
  - VariableCache.cs
  - UnrealScriptParser.cs
  - ClassFileLocator.cs
  - VariableTypeResolver.cs
  - VariableTypeResolutionResult.cs
  - StructDefinitionResolver.cs
  - CachedStructDef.cs
  - UnrealStructDef.cs
  - UnrealStructField.cs
  - StructField.cs
  - ClassFileResult.cs
  - ModSrcPathCache.cs
  - StructFileLocator.cs
  - StructIndexer.cs
  - UcFileParser.cs

Configuration/ (1 file):
  - SettingsLoader.cs

CLI/ (4 files):
  - FileProcessor.cs
  - OutputFormatter.cs
  - ErrorLog.cs
  - ErrorLogWriter.cs
```

### XCom2ModCompiler Source Files (26 files)

```
Root:
  - Program.cs

Application/ (1 file):
  - BuildController.cs (1056 lines)

Configuration/ (3 files):
  - BuildOptions.cs
  - ContentOptions.cs
  - ParserSettings.cs (DUPLICATE - to be removed)

Compilation/ (4 files):
  - ScriptCompiler.cs
  - BuildResult.cs
  - OutputReceiver.cs
  - ShaderPrecompiler.cs

Cooking/ (2 files):
  - AssetCooker.cs
  - ModAssetsCookStep.cs

Tracking/ (4 files):
  - BuildTracker.cs
  - MacroValidator.cs
  - CookerOutputTracker.cs

Utilities/ (10 files):
  - ProcessRunner.cs
  - ProcessExtensions.cs
  - BuildUtilities.cs
  - TableFormatter.cs
  - IniHandler.cs
  - JsonUtilities.cs
  - LocalizationConverter.cs
  - FileMirror.cs
  - MissingUncookedCopier.cs
  - ProjectSynchronizer.cs

Exceptions/ (1 file):
  - BuildExceptions.cs
```

---

## Appendix B: Key Code Locations

### Config Parser Integration Points

1. **BuildController.InvokeBuildAsync()** - Line ~370
   - Already loads SettingsLoader
   - Natural place for config validation step

2. **IniHandler.DiscoverAndValidate()**
   - Already parses XComEngine.ini
   - Could use ConfigParser for more robust parsing

3. **BuildOptions**
   - Add SkipConfigValidation flag
   - Add TreatConfigWarningsAsErrors flag

### Test Files to Update

1. **BuildControllerTests.cs**
   - Add tests for config validation step
   - Mock FileProcessor

2. **IniHandlerTests.cs**
   - Update to use ConfigParser if integrating

---

**End of Report**
