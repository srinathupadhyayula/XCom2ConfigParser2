# X2ModCompiler Unification - Quick Reference Guide

**For:** Developers implementing the unification  
**Companion to:** UNIFICATION_ANALYSIS_REPORT.md  
**Updated:** March 24, 2026 - .NET 10.0 + Modern Libraries

---

## Quick Start: Minimal Integration (3-4 hours)

If you want the fastest path to integration:

### Step 1: Update XCom2ModCompiler.csproj

```xml
<!-- Change these lines -->
<TargetFramework>net8.0-windows</TargetFramework>

<!-- To these -->
<TargetFramework>net10.0-windows</TargetFramework>
<LangVersion>13.0</LangVersion>
```

### Step 2: Update Spectre.Console Versions

```xml
<!-- Update these -->
<PackageReference Include="Spectre.Console" Version="0.48.0" />
<PackageReference Include="Spectre.Console.Cli" Version="0.48.0" />

<!-- To these -->
<PackageReference Include="Spectre.Console" Version="0.54.0" />
<PackageReference Include="Spectre.Console.Cli" Version="0.54.0" />
```

### Step 3: Add Reference to XCom2ConfigParser2

Edit `XCom2ModCompiler/XCom2ModCompiler.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\XCom2ConfigParser2\XCom2ConfigParser2.csproj" />
</ItemGroup>
```

### Step 4: Remove Duplicate ParserSettings

Delete `XCom2ModCompiler/Configuration/ParserSettings.cs`

Update `XCom2ModCompiler/Utilities/IniHandler.cs`:

```csharp
// Add this using
using XCom2ConfigParser2.Configuration;

// No other changes needed - ParserSettings is identical
```

### Step 5: Add Config Validation to BuildController

In `BuildController.InvokeBuildAsync()`, after line ~200 (after Preparation step):

```csharp
// 1.5 Validate Config Files
await PerformStepAsync(
    async () =>
    {
        _logger.ZLogInformation("Validating config files...");
        
        var settingsLoader = new SettingsLoader(_options.ProjectRoot);
        var settings = settingsLoader.Load();
        
        var fileProcessor = new FileProcessor(
            new SyntaxValidator(),
            settings,
            structValidationEnabled: true);
        
        var configFiles = new List<string>();
        foreach (var iniRoot in settings.IniRoots)
        {
            if (Directory.Exists(iniRoot))
            {
                configFiles.AddRange(Directory.GetFiles(iniRoot, "*.ini", SearchOption.AllDirectories));
            }
        }
        
        var hasErrors = false;
        foreach (var file in configFiles)
        {
            var result = fileProcessor.ProcessFile(file);
            foreach (var diagnostic in result.Diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    _logger.ZLogError("Config error in {File}: {Message}", file, diagnostic.Message);
                    hasErrors = true;
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

### Step 6: Build and Test

```powershell
# Build solution
dotnet build

# Run tests
dotnet test
```

**Done!** Config validation is now integrated into the build pipeline.

---

## Modern Library Stack 🆕

The unification is an opportunity to adopt modern, high-performance .NET libraries:

### Recommended Libraries

| Library | Version | Purpose | Benefit | Already Using? |
|---------|---------|---------|---------|----------------|
| **.NET** | 10.0 | Framework | Latest LTS, C# 13, Native AOT | ❌ No |
| **Spectre.Console.Cli** | 0.54.0 | CLI framework | Latest features, bug fixes | ❌ No |
| **ZLogger** | 2.5.10 | Logging | Zero-allocation (Cysharp) | ✅ Yes |
| **Kokuban** | 0.2.0 | Console coloring | Terminal string styling (Cysharp) | ✅ Yes |
| **MemoryPack** | 1.21.4 | Binary serialization | 10x faster than JSON (Cysharp) | ❌ No |
| **ZLinq** | 1.5.5 | Zero-allocation LINQ | 2-10x faster LINQ (optional) | ❌ No |
| **xUnit v3** | 3.0.0 | Testing | Modern API | ❌ No |
| **NSubstitute** | 5.1.0 | Mocking | Better than Moq | ❌ No |

**Not Recommended:**
- **ZString** - Not needed (ZLogger doesn't require it)
- **R3** - Overkill (no reactive streams needed)
- **UniTask** - Not needed (standard Task is fine for console apps)

### Optional: Add MemoryPack for Cache Serialization

```xml
<!-- XCom2ModCompiler.csproj or XCom2ConfigParser2.Core.csproj -->
<PackageReference Include="MemoryPack" Version="1.21.4" />
```

```csharp
// StructCache.cs - Using MemoryPack
using MemoryPack;

public void Save()
{
    var bytes = MemoryPackSerializer.Serialize(_cache);
    File.WriteAllBytes(_cachePath, bytes);
}

public void Load()
{
    var bytes = File.ReadAllBytes(_cachePath);
    _cache = MemoryPackSerializer.Deserialize<Dictionary<string, CachedStructDef>>(bytes);
}
```

**Benefits:**
- 5-10x faster save/load
- Smaller cache files
- Version-tolerant (handles schema changes)

### Optional: Add ZLinq for Performance-Critical LINQ

```xml
<!-- Only if profiling shows LINQ bottleneck -->
<PackageReference Include="ZLinq" Version="1.5.5" />
```

```csharp
using ZLinq;

// Zero-allocation LINQ (2-10x faster for large collections)
var result = array.AsValueEnumerable()
    .Where(x => x > 0)
    .Select(x => x * 2)
    .ToArray();
```

### Create Directory.Packages.props (Recommended)

```xml
<!-- Directory.Packages.props at solution root -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Core .NET -->
    <PackageVersion Include="Microsoft.Extensions.FileSystemGlobbing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="10.0.0" />
    <PackageVersion Include="System.Text.Json" Version="10.0.0" />
    
    <!-- Cysharp -->
    <PackageVersion Include="ZLogger" Version="2.5.10" />
    <PackageVersion Include="Kokuban" Version="0.2.0" />
    <PackageVersion Include="MemoryPack" Version="1.21.4" />
    <!-- Optional: ZLinq for zero-allocation LINQ -->
    <!-- <PackageVersion Include="ZLinq" Version="1.5.5" /> -->
    
    <!-- UI/CLI -->
    <PackageVersion Include="Spectre.Console" Version="0.54.0" />
    <PackageVersion Include="Spectre.Console.Cli" Version="0.54.0" />
    
    <!-- Testing -->
    <PackageVersion Include="xunit" Version="3.0.0" />
    <PackageVersion Include="NSubstitute" Version="5.1.0" />
  </ItemGroup>
</Project>
```

Then in your `.csproj` files:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="ZLogger" />
    <PackageReference Include="Kokuban" />
    <PackageReference Include="Spectre.Console.Cli" />
  </ItemGroup>
</Project>
```

---

## Quick Reference: Common Tasks

### Add New Config Validation Option

**1. Add to BuildOptions.cs:**
```csharp
public bool SkipConfigValidation { get; init; } = false;
```

**2. Add to BuildCommand in Program.cs:**
```csharp
[CommandOption("--skip-config-validation")]
[Description("Skip config file validation")]
public bool SkipConfigValidation { get; init; }
```

**3. Pass to BuildController:**
```csharp
// In BuildCommand.ExecuteAsync()
var options = new BuildOptions
{
    // ...
    SkipConfigValidation = settings.SkipConfigValidation
};
```

**4. Use in BuildController:**
```csharp
structValidationEnabled: !_options.SkipConfigValidation
```

### Fix Namespace Conflicts

If you see "ambiguous reference" errors:

```csharp
// Use alias to disambiguate
using ParserSettings = XCom2ConfigParser2.Configuration.ParserSettings;
```

### Update Test Project

If merging test projects:

```xml
<!-- X2ModCompiler.Tests.csproj -->
<ItemGroup>
  <ProjectReference Include="..\src\X2ModCompiler\X2ModCompiler.csproj" />
  <ProjectReference Include="..\src\XCom2ConfigParser2.Core\XCom2ConfigParser2.Core.csproj" />
</ItemGroup>
```

Move test files:
```
XCom2ConfigParser2.Tests/
  → X2ModCompiler.Tests/ConfigValidation/
```

---

## Troubleshooting

### Error: "The type or namespace name 'ParserSettings' could not be found"

**Cause:** Duplicate ParserSettings was removed but references remain

**Fix:**
```csharp
// Add this using
using XCom2ConfigParser2.Configuration;
```

### Error: "Cannot convert XCom2ConfigParser2.Core.Diagnostic to XCom2ModCompiler.Diagnostic"

**Cause:** Namespace conflict - both projects have Diagnostic types

**Fix:** Use fully qualified names or aliases:
```csharp
using ConfigDiagnostic = XCom2ConfigParser2.Core.Diagnostic;
```

### Warning: "Spectre.Console.Cli version conflict"

**Cause:** Different versions in different projects

**Fix:** Create `Directory.Packages.props`:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <SpectreConsoleVersion>0.53.1</SpectreConsoleVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Spectre.Console.Cli" Version="$(SpectreConsoleVersion)" />
  </ItemGroup>
</Project>
```

### Build Error: "Duplicate ParserSettings definition"

**Cause:** Forgot to remove duplicate from XCom2ModCompiler

**Fix:** Delete `XCom2ModCompiler/Configuration/ParserSettings.cs`

### Runtime Error: "Could not load file or assembly"

**Cause:** Missing project reference or NuGet package

**Fix:** Ensure all projects have proper references:
```xml
<ItemGroup>
  <ProjectReference Include="..\XCom2ConfigParser2.Core\XCom2ConfigParser2.Core.csproj" />
</ItemGroup>
```

---

## Code Snippets

### FileProcessor with Logging

```csharp
var fileProcessor = new FileProcessor(
    new SyntaxValidator(),
    settings,
    structValidationEnabled: true,
    logger: loggerFactory.CreateLogger<FileProcessor>());
```

### Config Validation Helper Method

```csharp
private async Task<bool> ValidateConfigFilesAsync(
    ParserSettings settings, 
    CancellationToken ct)
{
    var fileProcessor = new FileProcessor(
        new SyntaxValidator(),
        settings,
        structValidationEnabled: true);
    
    var configFiles = new List<string>();
    foreach (var iniRoot in settings.IniRoots)
    {
        if (Directory.Exists(iniRoot))
        {
            configFiles.AddRange(
                Directory.GetFiles(iniRoot, "*.ini", SearchOption.AllDirectories));
        }
    }
    
    var hasErrors = false;
    foreach (var file in configFiles)
    {
        if (ct.IsCancellationRequested)
            return false;
            
        var result = fileProcessor.ProcessFile(file);
        if (result.HasErrors)
        {
            hasErrors = true;
            foreach (var diagnostic in result.Diagnostics)
            {
                _logger.ZLogError(
                    "Config error in {File}: {Code} - {Message}",
                    file,
                    diagnostic.Code,
                    diagnostic.Message);
            }
        }
    }
    
    return !hasErrors;
}
```

### Build Step Template

```csharp
await PerformStepAsync(
    async () =>
    {
        // Your step logic here
        await DoSomethingAsync();
    },
    "Doing",      // Progress word (e.g., "Validating")
    "Done",       // Completed word (e.g., "Validated")
    "description", // What you're doing (e.g., "config files")
    timings);     // TimingRecord list
```

---

## Testing Checklist

After integration, verify:

- [ ] `dotnet build` succeeds with no errors
- [ ] `dotnet test` passes all tests
- [ ] Config validation runs during build
- [ ] Build fails on config errors
- [ ] `--skip-config-validation` flag works
- [ ] Standalone config parser CLI still works
- [ ] Struct validation works (if enabled)
- [ ] Incremental builds still work

---

## File Move Checklist

If restructuring into src/tests folders:

### Move These Folders:
```
XCom2ConfigParser2/           → src/XCom2ConfigParser2.Core/
XCom2ModCompiler/             → src/X2ModCompiler/
XCom2ConfigParser2.Tests/     → tests/XCom2ConfigParser2.Core.Tests/
XCom2ModCompiler.Tests/       → tests/X2ModCompiler.Tests/
```

### Update These Paths:
- Solution file (.slnx)
- Project references in .csproj files
- CI/CD scripts
- Publish scripts

### Keep These at Root:
- X2ModCompiler.slnx
- Directory.Build.props
- Directory.Packages.props
- README.md
- docs/

---

## Performance Considerations

### Config Validation Impact on Build Time

Typical impact: **2-5 seconds** for 50-100 config files

**Optimization tips:**
1. Enable struct caching (default behavior)
2. Skip struct validation for quick iterations: `--skip-struct-validation`
3. Use incremental builds (BuildTracker handles this)

### Memory Usage

Config parser uses ~50-100MB RAM during struct indexing

**Mitigation:**
- Cache is disk-backed (`.xcom2cache/`)
- Subsequent runs are faster

---

## Decision Log Template

Use this to track decisions during implementation:

```markdown
## Decision Log

### [Date] - [Decision]

**Context:** What prompted this decision

**Decision:** What was decided

**Consequences:** Expected impact

**Alternatives Considered:**
- Option A: ...
- Option B: ...
```

---

## Contact & Resources

- **Main Report:** UNIFICATION_ANALYSIS_REPORT.md
- **Original Projects:**
  - XCom2ConfigParser2: Config validation tool
  - XCom2ModCompiler: Mod build system
- **Documentation:** docs/ folder

---

**Last Updated:** March 24, 2026
