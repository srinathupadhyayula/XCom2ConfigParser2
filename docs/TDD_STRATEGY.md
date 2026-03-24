# X2ModCompiler Unification - TDD Strategy

**Document Version:** 1.0  
**Date:** March 24, 2026  
**Purpose:** Test-Driven Development strategy for unifying XCom2ConfigParser2 and XCom2ModCompiler

---

## Executive Summary

This document outlines a **Test-Driven Development (TDD)** strategy for unifying the XCom2ConfigParser2 and XCom2ModCompiler projects. The strategy ensures **zero regression** while enabling safe refactoring and modernization.

### Key Principles

1. **Red-Green-Refactor** - Write failing tests first, make them pass, then improve
2. **Zero Regression** - All existing tests must pass throughout the migration
3. **Incremental Migration** - Small, testable steps with validation at each phase
4. **Test Pyramid** - More unit tests, fewer integration tests, minimal E2E tests
5. **Modern Testing** - xUnit v3, NSubstitute, Shouldly for expressive assertions

---

## Part 1: Current Testing Landscape

### 1.1 Test Project Inventory

| Project | Test Files | Test Count | Framework | Mocking | Assertions |
|---------|-----------|------------|-----------|---------|------------|
| **XCom2ConfigParser2.Tests** | 16 | ~150 | xUnit 2.9.3 | NSubstitute 5.1.0 | Shouldly 4.3.0 |
| **XCom2ModCompiler.Tests** | 20 | ~200 | xUnit 2.9.3 | Moq 4.20.72 | xUnit Assert |

### 1.2 Test Coverage Analysis

#### XCom2ConfigParser2.Tests Coverage

| Category | Files | Coverage | Quality |
|----------|-------|----------|---------|
| **Parser Tests** | 4 | ✅ Good | High - edge cases covered |
| **Struct Validation** | 8 | ✅ Good | High - caching tested |
| **Validation Tests** | 1 | ⚠️ Partial | Medium - needs more error cases |
| **Configuration Tests** | 1 | ⚠️ Partial | Medium - needs path resolution tests |

#### XCom2ModCompiler.Tests Coverage

| Category | Files | Coverage | Quality |
|----------|-------|----------|---------|
| **BuildController Tests** | 5 | ⚠️ Partial | Medium - integration-heavy |
| **Compilation Tests** | 3 | ⚠️ Partial | Medium - needs mock commandlet |
| **Cooking Tests** | 1 | ❌ Poor | Low - hard to test without SDK |
| **Utility Tests** | 9 | ✅ Good | High - well isolated |
| **Tracking Tests** | 2 | ✅ Good | High - good mocking |

### 1.3 Testing Gaps Identified

**Critical Gaps:**
1. ❌ No integration tests for config validation in build pipeline
2. ❌ No tests for MemoryPack cache serialization (new)
3. ❌ No tests for .NET 10.0 compatibility
4. ❌ No performance regression tests
5. ❌ No tests for concurrent struct validation

**Medium Priority Gaps:**
1. ⚠️ Limited error message validation
2. ⚠️ No tests for large config files (>10,000 lines)
3. ⚠️ No tests for malformed Unicode in config files
4. ⚠️ Limited tests for path resolution edge cases

---

## Part 2: TDD Migration Strategy

### Phase 0: Test Infrastructure Setup (Before Migration)

**Duration:** 2-3 hours

#### Step 0.1: Create Unified Test Infrastructure

**Action:** Create shared test infrastructure before starting migration

```csharp
// tests/Common/TestBase.cs
public abstract class TestBase : IDisposable
{
    protected readonly ITestOutputHelper _output;
    protected readonly TempDirectory _tempDir;
    
    protected TestBase(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = new TempDirectory();
    }
    
    public void Dispose() => _tempDir.Dispose();
}

// tests/Common/TempDirectory.cs
public sealed class TempDirectory : IDisposable
{
    public string Path { get; }
    
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), 
            $"X2ModTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(Path);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}
```

**Tests to Write:**
- [ ] `TempDirectory_CreatesUniqueDirectory`
- [ ] `TempDirectory_DeletesOnDispose`

#### Step 0.2: Upgrade Test Framework

**Action:** Upgrade to xUnit v3 and standardize on NSubstitute

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="xunit" Version="3.0.0" />
<PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
<PackageVersion Include="NSubstitute" Version="5.1.0" />
<PackageVersion Include="Shouldly" Version="4.3.0" />
```

**Tests to Write:**
- [ ] Verify all existing tests pass after upgrade
- [ ] Document any breaking changes from xUnit v2 → v3

#### Step 0.3: Create Test Categories

**Action:** Categorize tests for better organization

```csharp
// tests/Common/TestCategories.cs
public static class TestCategories
{
    public const string Unit = "Unit";
    public const string Integration = "Integration";
    public const string Performance = "Performance";
    public const string Regression = "Regression";
}

// Usage
[Trait(TestCategories.Category, TestCategories.Unit)]
[Fact]
public void MyUnitTest() { }
```

---

### Phase 1: Library Extraction (Test-First Approach)

**Duration:** 4-6 hours

#### Step 1.1: Create XCom2ConfigParser2.Core Project

**TDD Cycle:**

1. **Red:** Write test for library structure
```csharp
// tests/XCom2ConfigParser2.Core.Tests/StructureTests.cs
[Trait(TestCategories.Category, TestCategories.Unit)]
[Fact]
public void CoreLibrary_HasExpectedPublicTypes()
{
    var assembly = typeof(FileProcessor).Assembly;
    
    var expectedTypes = new[]
    {
        typeof(FileProcessor),
        typeof(SyntaxValidator),
        typeof(StructMemberValidator),
        typeof(ParserSettings),
        typeof(SettingsLoader)
    };
    
    foreach (var type in expectedTypes)
    {
        assembly.GetType(type.FullName)
            .ShouldNotBeNull($"Type {type.FullName} should be public");
    }
}
```

2. **Green:** Create project with public types
3. **Refactor:** Ensure clean API surface

**Tests to Write:**
- [x] `CoreLibrary_HasExpectedPublicTypes`
- [ ] `CoreLibrary_DoesNotExposeInternalTypes`
- [ ] `CoreLibrary_HasNoConsoleDependencies`

#### Step 1.2: Extract FileProcessor with ILogger Support

**TDD Cycle:**

1. **Red:** Write test for new ILogger constructor
```csharp
[Trait(TestCategories.Category, TestCategories.Unit)]
[Fact]
public void FileProcessor_WithLogger_LogsProcessing()
{
    var logger = new Substitute.For<ILogger<FileProcessor>>();
    var processor = new FileProcessor(
        new SyntaxValidator(),
        new ParserSettings(),
        structValidationEnabled: false,
        logger: logger);
    
    var testFile = _tempDir.CreateFile("test.ini", "[Section]");
    processor.ProcessFile(testFile);
    
    logger.Received().LogInformation(
        Arg.Any<LogLevel>(),
        Arg.Is<string>(s => s.Contains("Processing")));
}
```

2. **Green:** Add optional ILogger parameter
3. **Refactor:** Make logger truly optional (null object pattern)

**Tests to Write:**
- [ ] `FileProcessor_WithLogger_LogsProcessing`
- [ ] `FileProcessor_WithoutLogger_DoesNotThrow`
- [ ] `FileProcessor_WithNullLogger_WorksCorrectly`

#### Step 1.3: Extract StructCache with MemoryPack

**TDD Cycle:**

1. **Red:** Write test for MemoryPack serialization
```csharp
[Trait(TestCategories.Category, TestCategories.Unit)]
[Fact]
public void StructCache_Serialization_RoundTrips()
{
    var original = new CachedStructDef
    {
        StructName = "TestStruct",
        Fields = new List<StructField>
        {
            new StructField { Name = "Field1", Type = "int" }
        },
        CacheTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    };
    
    var bytes = MemoryPackSerializer.Serialize(original);
    var deserialized = MemoryPackSerializer.Deserialize<CachedStructDef>(bytes);
    
    deserialized.StructName.ShouldBe(original.StructName);
    deserialized.Fields.Count.ShouldBe(original.Fields.Count);
}
```

2. **Green:** Add MemoryPack attributes and serialization
3. **Refactor:** Add error handling for corrupted cache files

**Tests to Write:**
- [ ] `StructCache_Serialization_RoundTrips`
- [ ] `StructCache_Serialization_HandlesNullFields`
- [ ] `StructCache_Serialization_HandlesCorruptedData`
- [ ] `StructCache_Load_WhenFileMissing_CreatesNewCache`
- [ ] `StructCache_Save_WhenDiskFull_ThrowsGracefully`

---

### Phase 2: Integration Tests (Build Pipeline)

**Duration:** 6-8 hours

#### Step 2.1: Test Config Validation Integration

**TDD Cycle:**

1. **Red:** Write integration test
```csharp
[Trait(TestCategories.Category, TestCategories.Integration)]
[Fact]
public async Task BuildController_WithInvalidConfig_FailsBuild()
{
    var mod = await _testFixture.CreateModWithConfig("InvalidConfig.ini");
    var controller = _testFixture.CreateBuildController(mod);
    
    var result = await controller.InvokeBuildAsync();
    
    result.Success.ShouldBeFalse();
    result.Errors.ShouldContain(e => e.Contains("Config validation failed"));
}
```

2. **Green:** Add config validation step to BuildController
3. **Refactor:** Extract config validation to separate service

**Tests to Write:**
- [ ] `BuildController_WithInvalidConfig_FailsBuild`
- [ ] `BuildController_WithValidConfig_Succeeds`
- [ ] `BuildController_WithSkipConfigValidation_IgnoresErrors`
- [ ] `BuildController_WithConfigWarnings_AsErrors_Fails`

#### Step 2.2: Test Config File Discovery

**TDD Cycle:**

1. **Red:** Write test for config file discovery
```csharp
[Trait(TestCategories.Category, TestCategories.Unit)]
[Fact]
public void ConfigValidator_DiscoversAllIniFiles()
{
    var settings = new ParserSettings
    {
        IniRoots = new[] { _tempDir.Path }
    };
    
    _tempDir.CreateFile("Config1.ini", "[Section]");
    _tempDir.CreateFile("Sub/Config2.ini", "[Section]");
    _tempDir.CreateFile("Ignore.txt", "[Section]");
    
    var validator = new ConfigValidator(settings);
    var files = validator.DiscoverFiles();
    
    files.Count.ShouldBe(2);
    files.ShouldContain(f => f.EndsWith("Config1.ini"));
    files.ShouldContain(f => f.EndsWith("Config2.ini"));
}
```

2. **Green:** Implement file discovery logic
3. **Refactor:** Make glob pattern configurable

**Tests to Write:**
- [ ] `ConfigValidator_DiscoversAllIniFiles`
- [ ] `ConfigValidator_HandlesMissingDirectories`
- [ ] `ConfigValidator_RespectsGlobPatterns`
- [ ] `ConfigValidator_DeduplicatesFiles`

---

### Phase 3: Performance Regression Tests

**Duration:** 4-6 hours

#### Step 3.1: Establish Performance Baselines

**TDD Cycle:**

1. **Red:** Write performance test
```csharp
[Trait(TestCategories.Category, TestCategories.Performance)]
[Fact]
public void StructParser_ParseLargeFile_WithinTimeLimit()
{
    var content = GenerateLargeConfigFile(10000); // 10k lines
    var directives = DirectiveTokenizer.Tokenize(content);
    
    var stopwatch = Stopwatch.StartNew();
    var validator = new SyntaxValidator();
    validator.Validate(content, directives, "test.ini");
    stopwatch.Stop();
    
    stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500); // < 500ms
}
```

2. **Green:** Optimize until test passes
3. **Refactor:** Document performance characteristics

**Tests to Write:**
- [ ] `StructParser_ParseLargeFile_WithinTimeLimit`
- [ ] `StructCache_Load_WithinTimeLimit`
- [ ] `BuildController_FullBuild_WithinTimeLimit`
- [ ] `MemoryPack_Serialization_FasterThanJson`

#### Step 3.2: Memory Allocation Tests

**TDD Cycle:**

1. **Red:** Write allocation test
```csharp
[Trait(TestCategories.Category, TestCategories.Performance)]
[Fact]
public void StructParser_Parse_ZeroAllocations()
{
    var content = "[Section]\nKey=Value";
    
    var allocations = BenchmarkDotNet.Diagnostics.Windows.EtwDiagnoser
        .MeasureAllocations(() => StructParser.Parse(content));
    
    allocations.ShouldBe(0);
}
```

2. **Green:** Refactor to eliminate allocations
3. **Refactor:** Use `Span<T>`, `struct`, `stackalloc`

**Tests to Write:**
- [ ] `StructParser_Parse_ZeroAllocations`
- [ ] `DirectiveTokenizer_Tokenize_MinimalAllocations`
- [ ] `FileProcessor_ProcessFile_NoMemoryLeaks`

---

### Phase 4: Migration Validation Tests

**Duration:** 3-4 hours

#### Step 4.1: Verify Existing Tests Pass

**Action:** Run all existing tests in unified solution

```powershell
# Run all tests
dotnet test --configuration Release --logger "console;verbosity=detailed"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov
```

**Checklist:**
- [ ] All XCom2ConfigParser2.Tests pass (150 tests)
- [ ] All XCom2ModCompiler.Tests pass (200 tests)
- [ ] Code coverage > 80%
- [ ] No test failures or warnings

#### Step 4.2: Cross-Platform Compatibility Tests

**TDD Cycle:**

1. **Red:** Write path resolution test
```csharp
[Trait(TestCategories.Category, TestCategories.Regression)]
[Theory]
[InlineData("Config/test.ini", "Config\\test.ini")]
[InlineData("Config\\test.ini", "Config\\test.ini")]
[InlineData("../Config/test.ini", "..\\Config\\test.ini")]
public void PathResolution_NormalizesPaths(string input, string expected)
{
    var settings = new ParserSettings();
    var normalized = settings.NormalizePath(input);
    
    normalized.ShouldBe(expected);
}
```

2. **Green:** Implement path normalization
3. **Refactor:** Use `Path.Combine`, `Path.GetRelativePath`

**Tests to Write:**
- [ ] `PathResolution_NormalizesPaths`
- [ ] `PathResolution_HandlesUncPaths`
- [ ] `PathResolution_HandlesLongPaths`

---

## Part 3: Test Templates & Patterns

### 3.1 Unit Test Template

```csharp
/// <summary>
/// Unit tests for [Component]
/// </summary>
[Trait(TestCategories.Category, TestCategories.Unit)]
public class [Component]Tests : TestBase
{
    private readonly [Component] _sut; // System Under Test
    private readonly MockRepository _mocks;
    
    public [Component]Tests(ITestOutputHelper output) : base(output)
    {
        _mocks = new MockRepository(null);
        _sut = new [Component]();
    }
    
    [Fact]
    public void Method_WhenCondition_ThenExpectedResult()
    {
        // Arrange
        var input = CreateTestInput();
        
        // Act
        var result = _sut.Method(input);
        
        // Assert
        result.ShouldBe(expected);
    }
    
    [Fact]
    public void Method_WhenInvalidInput_ThenThrowsException()
    {
        // Arrange
        var invalidInput = CreateInvalidInput();
        
        // Act & Assert
        Should.Throw<ArgumentException>(() => _sut.Method(invalidInput));
    }
}
```

### 3.2 Integration Test Template

```csharp
/// <summary>
/// Integration tests for [Component]
/// </summary>
[Trait(TestCategories.Category, TestCategories.Integration)]
public class [Component]IntegrationTests : TestBase, IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;
    
    public [Component]IntegrationTests(TestFixture fixture, ITestOutputHelper output) 
        : base(output)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task FullWorkflow_WhenExecuted_ThenSucceeds()
    {
        // Arrange
        var testData = await _fixture.CreateTestData();
        
        // Act
        var result = await _fixture.ExecuteWorkflow(testData);
        
        // Assert
        result.Success.ShouldBeTrue();
    }
}
```

### 3.3 Performance Test Template

```csharp
/// <summary>
/// Performance tests for [Component]
/// </summary>
[Trait(TestCategories.Category, TestCategories.Performance)]
public class [Component]PerformanceTests
{
    [Fact]
    public void ProcessLargeFile_WithinTimeLimit()
    {
        // Arrange
        var largeFile = GenerateTestFile(10000);
        var sut = new [Component]();
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        sut.Process(largeFile);
        stopwatch.Stop();
        
        // Assert
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);
    }
    
    [Fact]
    public void Process_ZeroAllocations()
    {
        // Arrange
        var input = CreateTestInput();
        
        // Act & Assert
        var allocations = MeasureAllocations(() => 
        {
            var sut = new [Component]();
            sut.Process(input);
        });
        
        allocations.ShouldBe(0);
    }
}
```

---

## Part 4: Continuous Integration

### 4.1 GitHub Actions Workflow

```yaml
# .github/workflows/ci.yml
name: CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  test:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET 10.0
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Run Unit Tests
      run: dotnet test --filter "Category=Unit" --no-build --logger "console;verbosity=normal"
    
    - name: Run Integration Tests
      run: dotnet test --filter "Category=Integration" --no-build --logger "console;verbosity=normal"
    
    - name: Run Performance Tests
      run: dotnet test --filter "Category=Performance" --no-build --logger "console;verbosity=normal"
    
    - name: Generate Coverage Report
      run: dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov
    
    - name: Upload Coverage
      uses: codecov/codecov-action@v4
      with:
        files: ./coverage.lcov
```

### 4.2 Test Reporting

```xml
<!-- Directory.Build.targets -->
<Project>
  <PropertyGroup>
    <CollectCoverage>true</CollectCoverage>
    <CoverletOutputFormat>lcov,json</CoverletOutputFormat>
    <CoverletOutput>$(MSBuildThisFileDirectory)coverage</CoverletOutput>
  </PropertyGroup>
</Project>
```

---

## Part 5: Test Checklist

### Pre-Migration Checklist

- [ ] All existing tests pass
- [ ] Test infrastructure created (TestBase, TempDirectory)
- [ ] Test framework upgraded (xUnit v3)
- [ ] Test categories defined
- [ ] CI/CD pipeline configured

### During Migration Checklist

- [ ] Library extraction tests pass
- [ ] Integration tests written and passing
- [ ] Performance baselines established
- [ ] Memory allocation tests passing
- [ ] Code coverage > 80%

### Post-Migration Checklist

- [ ] All 350+ tests passing
- [ ] No regression in build time
- [ ] No regression in config validation time
- [ ] Memory usage stable or improved
- [ ] Documentation updated

---

## Part 6: Example Test Implementations

### 6.1 FileProcessor Tests

```csharp
using Shouldly;
using NSubstitute;
using Microsoft.Extensions.Logging;
using XCom2ConfigParser2.CLI;
using XCom2ConfigParser2.Validation;
using XCom2ConfigParser2.Configuration;

namespace XCom2ConfigParser2.Core.Tests.CLI;

[Trait(TestCategories.Category, TestCategories.Unit)]
public class FileProcessorTests : TestBase
{
    private readonly FileProcessor _sut;
    private readonly ILogger<FileProcessor> _logger;
    
    public FileProcessorTests(ITestOutputHelper output) : base(output)
    {
        _logger = Substitute.For<ILogger<FileProcessor>>();
        _sut = new FileProcessor(
            new SyntaxValidator(),
            new ParserSettings(),
            structValidationEnabled: false,
            logger: _logger);
    }
    
    [Fact]
    public void ProcessFile_WhenValidConfig_ReturnsNoErrors()
    {
        // Arrange
        var content = @"[Engine.ScriptPackages]
+NonNativePackages=MyMod";
        var testFile = _tempDir.CreateFile("test.ini", content);
        
        // Act
        var result = _sut.ProcessFile(testFile);
        
        // Assert
        result.HasErrors.ShouldBeFalse();
        result.Diagnostics.ShouldBeEmpty();
    }
    
    [Fact]
    public void ProcessFile_WhenInvalidSyntax_ReturnsErrors()
    {
        // Arrange
        var content = @"[Invalid Section Header ]
Key=Value";
        var testFile = _tempDir.CreateFile("test.ini", content);
        
        // Act
        var result = _sut.ProcessFile(testFile);
        
        // Assert
        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => 
            d.Code == ErrorCode.MalformedHeader);
    }
    
    [Fact]
    public void ProcessFile_WithLogger_LogsProcessing()
    {
        // Arrange
        var content = "[Section]";
        var testFile = _tempDir.CreateFile("test.ini", content);
        
        // Act
        _sut.ProcessFile(testFile);
        
        // Assert
        _logger.Received().LogInformation(
            Arg.Any<LogLevel>(),
            Arg.Any<string>(),
            Arg.Any<object[]>());
    }
    
    [Fact]
    public void ProcessFile_WhenFileNotFound_ReturnsIoError()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_tempDir.Path, "missing.ini");
        
        // Act
        var result = _sut.ProcessFile(nonExistentFile);
        
        // Assert
        result.HasErrors.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => 
            d.Severity == DiagnosticSeverity.Error);
    }
    
    [Fact]
    public void ProcessFile_WhenEmptyFile_ReturnsNoDiagnostics()
    {
        // Arrange
        var testFile = _tempDir.CreateFile("empty.ini", "");
        
        // Act
        var result = _sut.ProcessFile(testFile);
        
        // Assert
        result.Diagnostics.ShouldBeEmpty();
    }
}
```

### 6.2 BuildController Integration Tests

```csharp
using Shouldly;
using XCom2ModCompiler.Application;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Exceptions;

namespace XCom2ModCompiler.Tests.Application;

[Trait(TestCategories.Category, TestCategories.Integration)]
public class BuildControllerIntegrationTests : IClassFixture<BuildTestFixture>
{
    private readonly BuildTestFixture _fixture;
    private readonly ITestOutputHelper _output;
    
    public BuildControllerIntegrationTests(
        BuildTestFixture fixture, 
        ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }
    
    [Fact]
    public async Task InvokeBuildAsync_WithValidMod_Succeeds()
    {
        // Arrange
        var mod = await _fixture.CreateValidMod();
        var controller = _fixture.CreateBuildController(mod);
        
        // Act
        var result = await controller.InvokeBuildAsync();
        
        // Assert
        result.Success.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }
    
    [Fact]
    public async Task InvokeBuildAsync_WithInvalidConfig_Fails()
    {
        // Arrange
        var mod = await _fixture.CreateModWithInvalidConfig();
        var controller = _fixture.CreateBuildController(mod);
        
        // Act
        var result = await controller.InvokeBuildAsync();
        
        // Assert
        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => 
            e.Contains("Config validation failed"));
    }
    
    [Fact]
    public async Task InvokeBuildAsync_WithSkipConfigValidation_IgnoresErrors()
    {
        // Arrange
        var mod = await _fixture.CreateModWithInvalidConfig();
        var controller = _fixture.CreateBuildController(mod, 
            skipConfigValidation: true);
        
        // Act
        var result = await controller.InvokeBuildAsync();
        
        // Assert
        // Note: May still fail for other reasons, but not config validation
        result.Errors.ShouldNotContain(e => 
            e.Contains("Config validation failed"));
    }
}
```

---

## Appendix A: Test Data Generators

```csharp
public static class TestDataGenerator
{
    public static string GenerateValidConfig(int sectionCount = 10)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < sectionCount; i++)
        {
            sb.AppendLine($"[Section{i}]");
            sb.AppendLine($"+Property{i}=Value{i}");
        }
        return sb.ToString();
    }
    
    public static string GenerateInvalidConfig()
    {
        return @"[Invalid Section Header ]
Key=Value
[Another Section]
Bad Key=Bad Value";
    }
    
    public static string GenerateLargeConfigFile(int lineCount)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < lineCount; i++)
        {
            sb.AppendLine($"[Section{i % 100}]");
            sb.AppendLine($"Property{i}=Value{i}");
        }
        return sb.ToString();
    }
}
```

---

## Appendix B: Mock Factories

```csharp
public static class MockFactories
{
    public static IValidator CreateMockValidator()
    {
        var mock = Substitute.For<IValidator>();
        mock.Validate(Arg.Any<string>(), Arg.Any<List<Directive>>(), Arg.Any<string>())
            .Returns(Array.Empty<Diagnostic>());
        return mock;
    }
    
    public static BuildTracker CreateMockTracker()
    {
        var mock = Substitute.For<BuildTracker>();
        mock.ShouldRebuildAsync(Arg.Any<BuildOptions>(), Arg.Any<string>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        return mock;
    }
}
```

---

**End of Document**
