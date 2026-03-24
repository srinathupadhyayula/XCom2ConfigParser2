# XCom2ModCompiler - System Design Document

## Document Information

| Attribute | Value |
|-----------|-------|
| **Version** | 1.0 |
| **Status** | Draft |
| **Created** | 2026-03-23 |
| **Last Updated** | 2026-03-23 |
| **Author** | XCom2Modding Community |

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [System Overview](#2-system-overview)
3. [Goals and Objectives](#3-goals-and-objectives)
4. [Scope](#4-scope)
5. [Constraints and Assumptions](#5-constraints-and-assumptions)
6. [Risk Assessment](#6-risk-assessment)
7. [Success Criteria](#7-success-criteria)

---

## 1. Executive Summary

### 1.1 Purpose

This document describes the system design for **XCom2ModCompiler**, a C#-based build system for XCOM 2 mods. The system replaces the existing PowerShell-based build scripts while maintaining compatibility with existing workflows and VS Code integration.

### 1.2 Background

The current XCOM 2 mod build system consists of PowerShell scripts (`build.ps1`, `build_common.ps1`, etc.) that orchestrate:
- Script compilation via Unreal Engine 3 commandlets
- Asset cooking and packaging
- File deployment to game directories
- Incremental build detection

While functional, the PowerShell-based system has limitations:
- Limited IDE support and refactoring capabilities
- Difficult to unit test
- Performance bottlenecks in file operations
- Steep learning curve for new contributors

### 1.3 Proposed Solution

XCom2ModCompiler provides:
- **C# class library** for core build logic
- **CLI executable** for direct invocation
- **PowerShell wrapper** for VS Code compatibility
- **Comprehensive documentation** for maintainers

---

## 2. System Overview

### 2.1 System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    VS Code / ModBuddy IDE                       │
│                    (Ctrl+Alt+B Build Command)                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    PowerShell Wrapper Layer                     │
│                    build.ps1 (thin wrapper)                     │
│                  Add-Type XCom2ModCompiler.dll                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  XCom2ModCompiler Core (C#)                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              BuildController (Orchestrator)               │  │
│  └───────────────────────────────────────────────────────────┘  │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌───────────┐  │
│  │   Script    │ │    Asset    │ │   File      │ │   Build   │  │
│  │  Compiler   │ │   Cooker    │ │   Mirror    │ │  Tracker  │  │
│  └─────────────┘ └─────────────┘ └─────────────┘ └───────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  External Tools & Services                      │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌───────────┐  │
│  │  XComGame   │ │  Robocopy   │ │   .NET      │ │   Steam   │  │
│  │   .com      │ │    .exe     │ │  File I/O   │ │  Workshop │  │
│  └─────────────┘ └─────────────┘ └─────────────┘ └───────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 System Components

| Component | Responsibility | Technology |
|-----------|---------------|------------|
| **BuildController** | Main orchestration, build pipeline | C# |
| **ScriptCompiler** | Make commandlet invocation | C# + Process |
| **AssetCooker** | Asset cooking coordination | C# + Process |
| **FileMirror** | Directory synchronization | C# / Robocopy |
| **BuildTracker** | Incremental build detection | C# + JSON |
| **MacroValidator** | Macro redefinition checking | C# + Regex |
| **PowerShell Wrapper** | VS Code integration | PowerShell |

### 2.3 Data Flow

```
User Action (Ctrl+Alt+B)
       │
       ▼
VS Code tasks.json
       │
       ▼
build.ps1 (PowerShell)
       │
       ▼
BuildController.InvokeBuildAsync()
       │
       ├─► CopyModToSdkAsync()
       ├─► ConvertLocalizationAsync()
       ├─► CopyToSrcAsync()
       ├─► RunMakeBaseAsync()
       ├─► RunMakeModAsync()
       ├─► PrecompileShadersAsync()
       ├─► RunCookAssetsAsync()
       └─► FinalCopyAsync()
       │
       ▼
Build Result (Success/Failure)
```

---

## 3. Goals and Objectives

### 3.1 Primary Goals

| ID | Goal | Priority | Success Metric |
|----|------|----------|----------------|
| G1 | Maintain VS Code compatibility | Critical | Ctrl+Alt+B works unchanged |
| G2 | Replicate all PowerShell features | Critical | 100% feature parity |
| G3 | Improve build performance | High | 20% faster builds |
| G4 | Enable unit testing | High | 80% code coverage |

### 3.2 Secondary Goals

| ID | Goal | Priority | Success Metric |
|----|------|----------|----------------|
| G5 | Improve error messages | Medium | User survey feedback |
| G6 | Add build progress reporting | Medium | Real-time progress UI |
| G7 | Support parallel operations | Low | 30% faster asset cooking |
| G8 | Add CI/CD integration | Low | GitHub Actions workflow |

### 3.3 Non-Goals

The following are explicitly **out of scope**:

- Cross-platform support (Windows only)
- Support for XCOM 1 or other games
- ModBuddy IDE replacement
- Steam Workshop publishing automation
- Real-time build monitoring UI

---

## 4. Scope

### 4.1 In Scope

| Feature | Description | Priority |
|---------|-------------|----------|
| **Script Compilation** | Make commandlet orchestration | Critical |
| **Asset Cooking** | Unreal Engine cooker integration | Critical |
| **File Deployment** | Staging and final copy | Critical |
| **Incremental Builds** | Timestamp-based detection | High |
| **Highlander Support** | Native package cooking | High |
| **Macro Validation** | Redefinition detection | Medium |
| **Shader Precompilation** | Shader cache management | Medium |
| **Clean Command** | Build artifact removal | Medium |

### 4.2 Out of Scope

| Feature | Description |
|---------|-------------|
| **ModBuddy Integration** | Visual Studio plugin development |
| **Workshop Publishing** | Steam API integration |
| **Dependency Management** | NuGet-like package manager |
| **Live Reloading** | Runtime mod reloading |
| **Multi-Game Support** | Support for non-XCOM2 games |

### 4.3 Interface Boundaries

| Interface | Direction | Description |
|-----------|-----------|-------------|
| **VS Code tasks.json** | Input | Build command invocation |
| **User config variables** | Input | SDK/Game paths from VS Code |
| **XComGame.com** | Output | Script compilation |
| **Robocopy.exe** | Output | File operations |
| **Steam Workshop** | Output | Mod deployment target |

---

## 5. Constraints and Assumptions

### 5.1 Technical Constraints

| ID | Constraint | Impact |
|----|------------|--------|
| C1 | Windows-only (Win32 API, Robocopy) | No Linux/Mac support |
| C2 | .NET 8.0 Windows | Requires .NET 8 runtime |
| C3 | Unreal Engine 3 commandlet format | Fixed argument structure |
| C4 | PowerShell 5.1+ required | Windows PowerShell compatibility |
| C5 | XCOM 2 WOTC SDK required | Dependent on external SDK |

### 5.2 Assumptions

| ID | Assumption | Risk if Invalid |
|----|------------|-----------------|
| A1 | Users have VS Code or ModBuddy | Build integration fails |
| A2 | SDK installed in default location | Path resolution fails |
| A3 | Steam Workshop content paths stable | Dependency loading fails |
| A4 | XComGame.com exit codes reliable | Error detection fails |
| A5 | Robocopy available on all systems | File operations fail |

### 5.3 Dependencies

| Dependency | Version | Purpose |
|------------|---------|---------|
| **.NET 8.0 Runtime** | 8.0.x | Execution environment |
| **XCOM 2 WOTC SDK** | Latest | Compilation target |
| **Unreal Engine 3** | Built-in | Commandlet host |
| **PowerShell** | 5.1+ | Wrapper scripts |
| **Robocopy** | Built-in | File operations |

---

## 6. Risk Assessment

### 6.1 Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **R1: VS Code integration breaks** | Low | Critical | Maintain thin PS wrapper |
| **R2: Build performance regression** | Medium | High | Benchmark during development |
| **R3: Async deadlocks in PS** | Medium | High | Use `Task.Run()` wrapper |
| **R4: Path translation errors** | Medium | Medium | Extensive unit tests |
| **R5: TFC tracking logic errors** | High | High | Side-by-side comparison |

### 6.2 Project Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **R6: Scope creep** | Medium | Medium | Strict adherence to SDD |
| **R7: Testing gaps** | High | Medium | Test plan before coding |
| **R8: Documentation lag** | High | Low | Docs-first approach |
| **R9: User adoption resistance** | Low | Medium | Backwards compatibility |

### 6.3 External Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **R10: SDK changes** | Low | High | Abstraction layer |
| **R11: Steam path changes** | Low | Low | Configurable paths |
| **R12: .NET breaking changes** | Low | Low | LTS version pinning |

---

## 7. Success Criteria

### 7.1 Functional Success

- [ ] All existing build configurations work (Default, Debug)
- [ ] Highlander mods build successfully
- [ ] Asset cooking produces identical output
- [ ] Incremental builds work correctly
- [ ] Clean command removes all artifacts
- [ ] VS Code Ctrl+Alt+B works unchanged

### 7.2 Performance Success

- [ ] Build time ≤ PowerShell version (baseline)
- [ ] Memory usage < 500MB during build
- [ ] File copy operations 20% faster
- [ ] Parallel operations where applicable

### 7.3 Quality Success

- [ ] 80% unit test code coverage
- [ ] Zero critical bugs in first release
- [ ] Documentation complete and accurate
- [ ] User acceptance testing passed

### 7.4 Adoption Success

- [ ] Zero breaking changes for existing users
- [ ] Migration guide published
- [ ] Sample projects updated
- [ ] Community feedback positive

---

## Appendix A: Glossary

| Term | Definition |
|------|------------|
| **Highlander** | Mod that replaces native game packages |
| **Make Commandlet** | Unreal Engine script compilation tool |
| **TFC** | Texture File Cache (cooked texture data) |
| **Seek-Free** | Optimized package format for runtime |
| **Staging** | Intermediate build directory |
| **Cooking** | Asset compilation for game engine |

---

## Appendix B: References

1. [X2ModBuildCommon GitHub Repository](https://github.com/X2CommunityCore/X2ModBuildCommon)
2. [Unreal Engine 3 Documentation](https://docs.unrealengine.com/)
3. [XCOM 2 Modding Wiki](https://xcom2modding.fandom.com/)
4. [.NET 8 Documentation](https://docs.microsoft.com/dotnet/)
5. [PowerShell Documentation](https://docs.microsoft.com/powershell/)

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-23 | XCom2Modding Community | Initial draft |
