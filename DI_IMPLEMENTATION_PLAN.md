# DI Container Implementation Plan
## X2ModCompiler Dependency Injection Migration

**Date:** March 26, 2026  
**Status:** Planning Phase  
**Priority:** HIGH

---

## 1. Executive Summary

This document outlines the migration plan from manual dependency creation to Microsoft.Extensions.DependencyInjection (DI) container for the X2ModCompiler application.

### Current State
- All dependencies manually created in `Program.cs` (12+ dependencies)
- `BuildController` constructor has 12 parameters (violates SRP)
- Difficult to test and maintain
- Tight coupling between components

### Target State
- Use `Microsoft.Extensions.DependencyInjection` for DI
- Centralized service registration
- Improved testability and maintainability
- Follow dependency inversion principle
- Use standard `Microsoft.Extensions.Logging.LogLevel` via `CompilerLogLevel` enum
- Configuration setting: `X2ModCompiler.logVerbosity` (not just `verbosity`)

---

## 2. Completed Improvements

### 2.1 Settings Naming Convention
- ✅ Renamed from `xcom.configParser.*` to `X2ModCompiler.*`
- ✅ Added `X2ModCompiler.logVerbosity` setting (clearer than `verbosity`)
- ✅ Created `CompilerLogLevel` enum that maps to `Microsoft.Extensions.Logging.LogLevel`

### 2.2 Log Level Integration
The `CompilerLogLevel` enum provides:
- Direct mapping to `Microsoft.Extensions.Logging.LogLevel`
- Full compatibility with ZLogger's built-in log level filtering
- Configuration via `X2ModCompiler.logVerbosity` in settings.json
- Values: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`

**Example settings.json:**
```json
{
    "X2ModCompiler.sdkRoot": "E:\\Games\\Steam\\XCOM2 SDK",
    "X2ModCompiler.logVerbosity": "Debug"  // Clear, descriptive name
}
```

---

## 3. Migration Phases

### Phase 1: Foundation (4-6 hours)
1. Add `Microsoft.Extensions.DependencyInjection` package
2. Create service registration extension methods
3. Create `BuildServices` facade class
4. Update `Program.cs` to use DI container

### Phase 2: Refactoring (4-6 hours)
1. Refactor `BuildController` to use `BuildServices` facade
2. Update all build steps to use DI
3. Update integration tests to use DI

### Phase 3: Testing & Validation (2-3 hours)
1. Run all tests to verify functionality
2. Fix any issues
3. Update documentation

---

## 3. Technical Design

### 3.1 Package Dependencies

Add to `X2ModCompiler.csproj`:
```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
```

### 3.2 Service Registration

Create `X2ModCompiler/DependencyInjection/ServiceCollectionExtensions.cs`:
```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddX2ModCompiler(this IServiceCollection services, BuildOptions options)
    {
        // Core services
        services.AddSingleton(options);
        services.AddSingleton<ILoggerFactory>(sp => 
            LoggerFactory.Create(builder => 
            {
                builder.AddZLoggerConsole();
                builder.SetMinimumLevel(options.Debug ? LogLevel.Debug : LogLevel.Information);
            }));
        
        // Infrastructure
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IFileMirrorParity, ModernFileMirror>();
        
        // Build services
        services.AddSingleton<BuildTracker>();
        services.AddSingleton<ScriptCompiler>();
        services.AddSingleton<AssetCooker>();
        services.AddSingleton<ShaderPrecompiler>();
        services.AddSingleton<MissingUncookedCopier>();
        services.AddSingleton<ProjectSynchronizer>();
        services.AddSingleton<FileProcessor>();
        services.AddSingleton<ScriptCleaner>();
        
        // Controller
        services.AddSingleton<BuildController>();
        
        return services;
    }
}
```

### 3.3 BuildServices Facade

Create `X2ModCompiler/Application/BuildServices.cs`:
```csharp
/// <summary>
/// Facade for build-related services to reduce BuildController constructor parameters.
/// </summary>
public sealed class BuildServices
{
    public BuildTracker Tracker { get; }
    public ScriptCompiler Compiler { get; }
    public AssetCooker Cooker { get; }
    public IFileMirrorParity Mirror { get; }
    public IProcessRunner ProcessRunner { get; }
    public ShaderPrecompiler ShaderPrecompiler { get; }
    public MissingUncookedCopier MissingUncookedCopier { get; }
    public ProjectSynchronizer ProjectSynchronizer { get; }
    public FileProcessor FileProcessor { get; }
    public ScriptCleaner ScriptCleaner { get; }

    public BuildServices(
        BuildTracker tracker,
        ScriptCompiler compiler,
        AssetCooker cooker,
        IFileMirrorParity mirror,
        IProcessRunner processRunner,
        ShaderPrecompiler shaderPrecompiler,
        MissingUncookedCopier missingUncookedCopier,
        ProjectSynchronizer projectSynchronizer,
        FileProcessor fileProcessor,
        ScriptCleaner scriptCleaner)
    {
        Tracker = tracker;
        Compiler = compiler;
        Cooker = cooker;
        Mirror = mirror;
        ProcessRunner = processRunner;
        ShaderPrecompiler = shaderPrecompiler;
        MissingUncookedCopier = missingUncookedCopier;
        ProjectSynchronizer = projectSynchronizer;
        FileProcessor = fileProcessor;
        ScriptCleaner = scriptCleaner;
    }
}
```

### 3.4 Program.cs Refactoring

**Before:**
```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddZLoggerConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

options.SyncToParserSettings();
var syntaxValidator = new SyntaxValidator();
var modSrcCache = new ModSrcPathCache(options.ParserSettings, loggerFactory.CreateLogger<ModSrcPathCache>());
var fileProcessor = new FileProcessor(syntaxValidator, options.ParserSettings, structValidationEnabled: true, loggerFactory, modSrcCache);
var runner = new ProcessRunner(loggerFactory.CreateLogger<ProcessRunner>());
var mirror = new ModernFileMirror(runner);
// ... 10+ more instantiations

var controller = new BuildController(options, loggerFactory, tracker, compiler, cooker, mirror, runner, ...);
```

**After:**
```csharp
var services = new ServiceCollection();
services.AddX2ModCompiler(options);

var serviceProvider = services.BuildServiceProvider();
var controller = serviceProvider.GetRequiredService<BuildController>();
```

### 3.5 BuildController Constructor

**Before:**
```csharp
public BuildController(
    BuildOptions options,
    ILoggerFactory loggerFactory,
    BuildTracker tracker,
    ScriptCompiler compiler,
    AssetCooker cooker,
    IFileMirrorParity mirror,
    IProcessRunner processRunner,
    ShaderPrecompiler shaderPrecompiler,
    MissingUncookedCopier missingUncookedCopier,
    ProjectSynchronizer projectSynchronizer,
    FileProcessor fileProcessor,
    ScriptCleaner scriptCleaner)
```

**After:**
```csharp
public BuildController(
    BuildOptions options,
    ILoggerFactory loggerFactory,
    BuildServices services)
```

---

## 4. Testing Strategy

### 4.1 Unit Tests
- No changes required (already use mocks)
- Tests already follow DI patterns with mocks

### 4.2 Integration Tests
- Update test setup to use `ServiceCollection`
- Replace manual mock creation with `IServiceProvider`

**Example:**
```csharp
[Fact]
public async Task BuildController_ExecutesPipeline_WhenSuccessful()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddX2ModCompiler(options);
    
    // Register mocks
    services.AddSingleton(_compilerMock.Object);
    services.AddSingleton(_mirrorMock.Object);
    
    var serviceProvider = services.BuildServiceProvider();
    var controller = serviceProvider.GetRequiredService<BuildController>();
    
    // Act
    var result = await controller.InvokeBuildAsync();
    
    // Assert
    Assert.True(result.Success);
}
```

---

## 5. Risk Assessment

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| Breaking changes to public API | Medium | Low | Keep internal, no public API changes |
| Test failures | Medium | Medium | Comprehensive test suite, run tests after each change |
| Performance overhead | Low | Low | DI container overhead is negligible |
| Circular dependencies | Low | Low | Careful service registration order |

---

## 6. Rollback Plan

If issues arise:
1. Revert `Program.cs` changes
2. Revert `BuildController` constructor changes
3. Remove `BuildServices` class
4. Remove DI package reference

All changes are additive until final switchover, allowing incremental rollback.

---

## 7. Success Criteria

- [ ] All 311 tests passing
- [ ] No manual dependency creation in `Program.cs`
- [ ] `BuildController` constructor has ≤5 parameters
- [ ] All services registered in DI container
- [ ] Build time unchanged or improved
- [ ] Code coverage maintained or improved

---

## 8. Implementation Checklist

### Phase 1: Foundation
- [ ] Add `Microsoft.Extensions.DependencyInjection` package
- [ ] Create `ServiceCollectionExtensions.cs`
- [ ] Create `BuildServices.cs` facade
- [ ] Update `Program.cs` to use DI
- [ ] Verify build succeeds

### Phase 2: Refactoring
- [ ] Update `BuildController` constructor
- [ ] Update all build steps
- [ ] Update integration tests
- [ ] Verify all tests pass

### Phase 3: Validation
- [ ] Run full test suite (311 tests)
- [ ] Performance benchmark
- [ ] Update documentation
- [ ] Code review

---

## 9. File Change Summary

### New Files
1. `X2ModCompiler/DependencyInjection/ServiceCollectionExtensions.cs`
2. `X2ModCompiler/Application/BuildServices.cs`

### Modified Files
1. `X2ModCompiler/X2ModCompiler.csproj` - Add DI package
2. `X2ModCompiler/Program.cs` - Use DI container
3. `X2ModCompiler/Application/BuildController.cs` - Use BuildServices facade
4. `X2ModCompiler.Tests/*.cs` - Update test setup (multiple files)

---

## 10. Timeline Estimate

| Phase | Estimated Time | Dependencies |
|-------|---------------|--------------|
| Phase 1: Foundation | 4-6 hours | None |
| Phase 2: Refactoring | 4-6 hours | Phase 1 complete |
| Phase 3: Validation | 2-3 hours | Phase 2 complete |
| **Total** | **10-15 hours** | |

---

**Approved by:** AI Code Review Assistant  
**Next Steps:** Begin Phase 1 implementation
