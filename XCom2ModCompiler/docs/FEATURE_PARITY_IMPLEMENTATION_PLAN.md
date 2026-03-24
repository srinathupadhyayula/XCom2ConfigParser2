# XCom2ModCompiler - 100% Feature Parity Implementation Plan

**Goal:** Achieve 100% feature parity with build_common.ps1

**Source:** MISSING_LOGGING_FEATURES_ANALYSIS.md (47 missing features identified)

---

## Implementation Priority

### P0 - Core Build Pipeline Parity (Must Complete)
These features are essential for the build to work identically to PowerShell version.

### P1 - Logging & Output Parity (Must Complete)  
These features ensure identical user-visible output and logging.

### P2 - Error Handling Parity (Must Complete)
These features ensure identical error detection and reporting.

### P3 - Edge Case Parity (Should Complete)
These features handle edge cases the PowerShell version handles.

---

## Feature Implementation Checklist

### P0 - Core Build Pipeline Parity

#### 1. Build Step Execution
- [x] ✅ PerformStepAsync pattern with timing
- [x] ✅ Progress/completed word formatting
- [x] ✅ Step timing records

#### 2. Output Receivers
- [x] ✅ PassthroughReceiver
- [x] ✅ BufferingReceiver (buffer then show on failure)
- [x] ✅ ModcookReceiver (GFx spam filtering)
- [x] ✅ MakeOutputReceiver (path translation + color coding)

#### 3. Process Execution
- [x] ✅ RunProcessWithSleepAsync (sleep at start/end)
- [x] ✅ Event-based output handling
- [x] ✅ Process tree killing on cancellation

#### 4. Highlander Support
- [x] ✅ HasNativePackages() detection
- [x] ✅ CopyScriptPackagesAsync() with native/non-native handling
- [x] ✅ CleanLeftoverScriptsAsync()

#### 5. Mod Metadata
- [x] ✅ .XComMod file generation from x2proj

#### 6. Asset Cooking
- [ ] ❌ Complete ModAssetsCookStep implementation
- [ ] ❌ TFC file tracking
- [ ] ❌ SF package tracking
- [ ] ❌ TFC growth detection and warning
- [ ] ❌ Engine.ini preparation with additions
- [ ] ❌ Collection map cooking
- [ ] ❌ Dirty maps detection (incremental cooking)
- [ ] ❌ Cooker output tracker persistence (JSON)

#### 7. Selective Clean
- [ ] ❌ CheckCleanCompiled() - selective clean based on fingerprints
- [ ] ❌ Globals.uci hash tracking
- [ ] ❌ Core.u timestamp tracking
- [ ] ❌ Build mode switch detection (debug/release)

#### 8. Macro Validation
- [ ] ❌ X2MBC-Redefine escape hatch support
- [ ] ❌ Detailed macro redefinition errors with file:line references
- [ ] ❌ Help text for fixing errors

### P1 - Logging & Output Parity

#### 9. Detailed Step Logging
- [ ] ❌ ItemGroup regeneration detailed logging
  - "Clearing old ItemGroups..."
  - "Removed Folder/Content 'X' from ItemGroups"
  - "Scanning project directory..."
  - "Found X files and Y folders"
  - "Added Folder/Content 'X'"
- [ ] ❌ Mod copy to SDK logging
  - "Copying mod project to staging..."
  - "Copied project to staging"
  - "Reading mod metadata from X"
  - "Read."
  - "Writing mod metadata..."
  - "Written."
- [ ] ❌ Source copying logging
  - "Mirroring SrcOrig to Src..."
  - "Mirrored SrcOrig to Src"
  - "Copying dependency sources to Src..."
  - List each dependency folder name
  - "Copied dependency sources to Src"
  - "Copying the mod's sources to Src..."
  - "Copied mod sources to Src"

#### 10. Path Validation
- [ ] ❌ Echo SDK and Game paths at start
- [ ] ❌ Helpful error messages about user config variables
- [ ] ❌ Path existence checks with helpful messages

#### 11. Timing Reports
- [x] ✅ X2MC_REPORT_TIMINGS environment variable
- [x] ✅ Total duration calculation
- [x] ✅ Unaccounted time calculation
- [ ] ❌ Formatted table output with Share percentage

#### 12. Success/Failure Messages
- [x] ✅ PlaySuccessSound/PlayFailureSound (stubbed)
- [ ] ❌ SuccessMessage with mod name and "ready to run"
- [ ] ❌ FailureMessage with red color

#### 13. Utility Functions
- [x] ✅ FormatFileSize()
- [x] ✅ FormatElapsed()

### P2 - Error Handling Parity

#### 14. Flag Validation
- [ ] ❌ _CheckFlags() - debug and final_release conflict detection

#### 15. Path Confirmation
- [ ] ❌ _ConfirmPaths() with detailed error messages
- [ ] ❌ Check for unresolved config placeholders

#### 16. Content Options Validation
- [ ] ❌ Validate ContentForCook exists when cooking requested
- [ ] ❌ Check SDK ContentMods directory not in use
- [ ] ❌ Verify shipped GPCD exists

#### 17. TFC Verification
- [ ] ❌ Verify cached TFCs not altered (full recook trigger)
- [ ] ❌ Verify cached SF packages not altered

### P3 - Edge Case Parity

#### 18. Pre-Make Hooks
- [ ] ❌ AddPreMakeHook() support
- [ ] ❌ _RunPreMakeHooks() execution
- [ ] ❌ Validation: no hooks when no script packages

#### 19. Clean Additional Mods
- [ ] ❌ AddToClean() support
- [ ] ❌ _CleanAdditional() - clean other mods before build
- [ ] ❌ Validation: no clean when no script packages

#### 20. Final Release Handling
- [ ] ❌ Build base twice when final_release (once with, once without)
- [ ] ❌ Skip Highlander cooking in debug mode
- [ ] ❌ Validation: final_release only for Highlander mods

#### 21. Shader Precompilation
- [x] ✅ Basic shader precompilation
- [ ] ❌ Detailed "Checking the need to PrecompileShaders" logging
- [ ] ❌ "No content files, skipping PrecompileShaders"
- [ ] ❌ "No reason to precompile shaders, using existing"
- [ ] ❌ "Generated Shader Cache"

#### 22. Localization Conversion
- [x] ✅ UTF-8 to UTF-16 conversion
- [ ] ❌ Detailed file-by-file logging

#### 23. Build Cache Management
- [ ] ❌ Create BuildCache directory if missing
- [ ] ❌ lastBuildDetails.json initialization with default properties
- [ ] ❌ CompiledModPackages.txt manifest generation

#### 24. Script Package Detection
- [ ] ❌ _HasScriptPackages() validation
- [ ] ❌ _ShouldCompileBase() logic (compile base if cooking assets)
- [ ] ❌ Validation errors for debug/clean/include without packages

#### 25. Engine.ini Management
- [ ] ❌ Read SDK Engine.ini content
- [ ] ❌ _PrepareBuildCacheEngineIniWithAdditions()
- [ ] ❌ Assets cooking Engine.ini with:
  - ContentForCook path injection
  - Collection maps path injection
  - Skip directory enumeration
  - Collection map package forcing

#### 26. Collection Maps
- [ ] ❌ EmptyUMap extraction from embedded resources
- [ ] ❌ Collection map creation for missing maps
- [ ] ❌ Collection map cooking with forced packages

#### 27. Artifact Staging
- [ ] ❌ TFC file staging
- [ ] ❌ Map staging
- [ ] ❌ SF package staging with _SF suffix removal

#### 28. Final Copy
- [ ] ❌ Robocopy with proper arguments
- [ ] ❌ Exit code checking (0-7 = success)
- [ ] ❌ Source verification before deletion
- [ ] ❌ Source directory removal after successful copy
- [ ] ❌ Warning messages on failure

---

## Implementation Status

| Category | P0 | P1 | P2 | P3 | Total |
|----------|----|----|----|----|-------|
| **Completed** | 11 | 5 | 0 | 2 | 18 |
| **Remaining** | 10 | 8 | 4 | 7 | 29 |
| **Total** | 21 | 13 | 4 | 9 | **47** |

**Current Progress: 18/47 (38%)**

---

## Next Steps (In Order)

1. **Complete P0 items** - Asset cooking pipeline, selective clean, macro validation
2. **Complete P1 items** - Detailed logging, timing reports, success/failure messages
3. **Complete P2 items** - Error handling, validation
4. **Complete P3 items** - Edge cases

---

*Last updated: 2026-03-23*
