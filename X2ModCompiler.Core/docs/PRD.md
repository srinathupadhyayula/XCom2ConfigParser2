# Product Requirements Document (PRD)
## UE3 Config Parser & Validator (Desktop CLI)

**Version:** 1.0  
**Date:** 2026-03-22  
**Platform:** Windows Desktop (.exe)  
**Language:** C# (.NET 8) or C++20

---

## 1. Executive Summary

### 1.1 Product Vision
A command-line tool that parses and validates Unreal Engine 3 (UE3) configuration files (.ini), detecting syntax errors and reporting them with precise line/column positions.

### 1.2 Problem Statement
UE3 config files use a custom INI-like format with:
- Multi-line value continuation (`\\`)
- Array/struct literal syntax
- KVP operation prefixes (`+`, `.`, `-`, `!`)
- Strict identifier rules

Standard INI parsers cannot handle this. Users need validation before game config changes.

### 1.3 Target Users
- Game modders editing XCOM 2, Mass Effect, or other UE3 game configs
- Developers debugging config loading issues
- CI/CD pipelines validating config files

---

## 2. Functional Requirements

### 2.1 Core Features

| ID | Feature | Priority | Description |
|----|---------|----------|-------------|
| F1 | Parse INI files | P0 | Read and tokenize UE3 .ini files |
| F2 | Validate syntax | P0 | Check all directives against UE3 grammar rules |
| F3 | Report errors | P0 | Output errors with file, line, column, message |
| F4 | Batch processing | P1 | Process entire directories recursively |
| F5 | Exit codes | P1 | Return non-zero exit code on errors |
| F6 | Struct member validation | P1 | Validate struct field names against UnrealScript definitions |

### 2.2 Non-Functional Requirements

| ID | Requirement | Target |
|----|-------------|--------|
| NF1 | Performance | Parse 100KB file in <100ms |
| NF2 | Memory | <50MB peak for 10MB input file |
| NF3 | Encoding | UTF-8 only (reject invalid UTF-8 with error) |
| NF4 | CLI | Single .exe, no runtime installation required |
| NF5 | Cross-platform | Windows primary; Linux/macOS optional |

---

## 3. Input Specification

### 3.1 File Format
- **Extension:** `.ini` (convention; tool processes any text file)
- **Encoding:** UTF-8 (with or without BOM)
- **Line endings:** `\r\n` (Windows), `\n` (Unix), `\r` (legacy Mac)

### 3.2 Grammar Overview

```
file          ::= (directive | newline)*
directive     ::= section_header | kvp | comment | unknown
section_header ::= '[' object_name ']'
object_name   ::= IDENT (' ' IDENT)?
kvp           ::= [op] property_name '=' value
op            ::= '' | '+' | '.' | '-' | '!'
property_name ::= IDENT ('[' NUM ']' | '(' NUM ')')?
value         ::= terminal | struct | array
terminal      ::= STRING | NUMBER | BOOLEAN | IDENT
struct        ::= '(' (prop_assign (',' prop_assign)*)? ')'
array         ::= '(' (terminal (',' terminal)*) ')'
comment       ::= ';' TEXT
unknown       ::= any line not matching above
```

### 3.3 Token Definitions

| Token | Regex | Examples |
|-------|-------|----------|
| IDENT | `[A-Za-z][A-Za-z0-9_]*` | `MyProp`, `_invalid`, `123invalid` |
| NUM | `[0-9]+` (no leading zeros for indices) | `0`, `10`, `01` (invalid) |
| STRING | `".*?"` | `"Hello"`, `"Path\\To\\File"` |
| BOOLEAN | `true` \| `false` (case-insensitive) | `True`, `FALSE`, `true` |
| NUMBER | `-?[0-9]+\.?[0-9]*` | `42`, `-5`, `3.14`, `0.5` |

### 3.4 KVP Operation Prefixes

| Prefix | Enum Value | Semantic Meaning |
|--------|------------|------------------|
| (none) | `Set` | Replace existing value |
| `+` | `InsertUnique` | Add if not already present |
| `.` | `Insert` | Append to array |
| `-` | `Remove` | Remove matching entry |
| `!` | `Clear` | Clear array before operation |

### 3.5 Multi-line Continuation

```ini
+MyArray=(Item="First", \
    Item2="Second", \
    Item3="Third")
```

- Line ending with `\\` continues to next line
- Whitespace after `\\` before newline = **error**
- `\\` at EOF without following line = **error**
- Continuation lines are joined with two spaces `  `

### 3.6 Struct Syntax

```ini
+MyStruct=(Prop1="Value", Prop2[0]=(Nested="Deep"), Prop3=123)
```

Rules:
- Opens with `(`, closes with `)`
- Children are `PropertyName=Value` pairs
- Property names can have `[index]` suffix
- Values can be: terminal, nested struct, or array
- Empty struct: `()`

### 3.7 Array Syntax

```ini
+MyArray=(Item1, Item2, Item3)
+MyArrayOfStructs=((A=1), (A=2), (A=3))
```

Rules:
- Opens with `(`, closes with `)`
- Elements separated by `,`
- Elements are terminals OR structs (no nested arrays)
- Array of structs: each struct wrapped in `()`

---

## 4. Validation Rules

### 4.1 Error Types

| Error Code | Trigger | Message | Severity |
|------------|---------|---------|----------|
| `InvalidIdent` | Property/section name fails regex | `Invalid identifier` | Error |
| `MalformedHeader` | Line looks like `[header]` but invalid | `Invalid header. The first character of a header line must be \`[\` and the last must be \`]`.` | Error |
| `SpaceAfterMultiline` | Space/tab after `\\` continuation | `Unrecognized directive (space after backslashes)` | Error |
| `SlashSlashComment` | Line starts with `//` | `UnrealScript-style comment (please use \`;\`)` | Error |
| `BadValue` | Value doesn't match any valid type | `Bad Value` | Error |
| `TrailingContinuation` | `\\` at end of file | `Trailing \\ without following line` | Error |
| `StructParseError` | Struct syntax error | Context-specific (see 4.2) | Error |
| `InvalidStructMember` | Struct field name not in UnrealScript definition | Context-specific (see 4.5) | Error |
| `StructDefNotFound` | Struct definition could not be resolved | Context-specific (see 4.5) | Warning |
| `Other` | Unknown line not matching any pattern | `Invalid config directive` | Error |

### 4.2 Struct Parse Error Messages

| Condition | Error Message |
|-----------|---------------|
| Missing `(` at struct start | `Expected \`(\`` |
| Missing property name | `Expected property name` |
| Missing `=` after property name | `Expected \`=\`` |
| Missing value after `=` | `Expected \`(\` or value` |
| Missing `]` after array index | `Expected \`]` |
| Invalid array index (non-numeric) | `Expected array index` |
| Missing `,` or `)` between entries | `Expected \`,\` or \`)` |
| Missing value in array | `Expected value` |
| Unexpected tokens after struct end | `Expected end of tokens` |

### 4.3 Comment Handling

| Pattern | Result |
|---------|--------|
| `; comment text` | Valid comment, ignored |
| `// comment text` | Error: `SlashSlashComment` |
| `property=value ; comment` | Valid (comment after value) |
| `[header] ; comment` | Valid (comment after header) |

### 4.4 Whitespace Rules

| Location | Rule |
|----------|------|
| Before section header | Trimmed, header still valid |
| After section header | `] ` = malformed header error |
| Before property name | Trimmed |
| Around `=` | Trimmed |
| In struct/array | Spaces and tabs ignored between tokens |
| After `\\` | Error if any whitespace before newline |

### 4.5 Struct Member Validation

#### Problem

The UE3 engine produces runtime "Redscreen" errors when a config file references an unknown struct member. For example, using `Group` (singular) instead of `Groups` (plural) in a struct literal causes the engine to emit:

```
[0022.03] Error: Redscreen: ImportText (SDLReplacements): Unknown member Group in:
  (List="AdventSectoidOnlyLeaders", Duplicates=AllowDuplicates,
    Group[0] = (Group = "ADVENTSectoidLeaders", SpawnWeightMultiplier=1), ...)
```

These errors are only visible at runtime and can be difficult to diagnose. The parser must detect them at validation time.

#### Goal

When the parser encounters a config entry containing an inline struct (a value using UE3's `(Key=Value, ...)` syntax), it validates the struct's field names against the actual UnrealScript struct definition. This catches typos and incorrect field names before the game is run.

#### Two-Phase Resolution Chain

The struct resolution process has two distinct phases:

| Phase | Input | Output | Description |
|-------|-------|--------|-------------|
| **1. Variable Type Resolution** | `[PackageName.ClassName]` + property name | Variable type (e.g., `SDLReplacement[]`) | Find the variable declaration to determine its type |
| **2. Struct Definition Resolution** | Struct type name (e.g., `SDLReplacement`) | Complete struct definition with all nested structs | Find and recursively map the struct definition |

---

#### Phase 1: Variable Type Resolution

**Step 1.1 — Parse Section Header:**
Extract `PackageName` and `ClassName` from the section header `[PackageName.ClassName]` immediately above the config entry.

**Step 1.2 — Locate Class File:**
Search for the class file using the following ordered chain. Stop at first match:

| Order | Source | Search Path |
|-------|--------|-------------|
| 1 | **Project sources** | `<project-root>/Src/<PackageName>/Classes/<ClassName>.uc` |
| 2 | **Dependent mod sources** | Parse `.scripts/build.ps1` for `$builder.IncludeSrc("<path>")`; search `<path>/<PackageName>/Classes/<ClassName>.uc` |
| 3 | **Community Highlander** | `~/.xcom2/mods/CommunityHighlander/Src/<PackageName>/Classes/<ClassName>.uc` |
| 4 | **Alien Highlander** | `~/.xcom2/mods/AlienHighlander/Src/<PackageName>/Classes/<ClassName>.uc` |
| 5 | **SDK sources** | Read `.vscode/settings.json` key `xcom.highlander.sdkroot`; search `<sdkroot>/Development/SrcOrig/<PackageName>/Classes/<ClassName>.uc` |

**Step 1.3 — Extract Variable Type:**
Parse the `.uc` file to find the variable declaration matching the property name:

```unrealscript
// In Classes/ELPSDLProcessor.uc
class ELPSDLProcessor extends Object;

var SDLReplacement[] SDLReplacements;  // ← Variable declaration
var int MaxCount;
```

Extract the type from the declaration:
- `SDLReplacement[]` → base type `SDLReplacement` (array of struct)
- `int` → primitive type (no struct validation needed)
- `MyStruct` → base type `MyStruct` (single struct)

**Output:** Struct type name (e.g., `SDLReplacement`)

---

#### Phase 2: Struct Definition Resolution

**Step 2.1 — Check Local Cache:**
Before searching, check the struct cache at `.xcom2cache/structs/<StructName>.json`. If the cache exists and is valid (source file hash unchanged, less than 24 hours old), use cached definition.

**Step 2.2 — Search for Struct Definition:**
If cache miss, search for `struct <StructName>` definition using the same ordered chain:

| Order | Source | Search Pattern |
|-------|--------|----------------|
| 1 | **Local cache** | `.xcom2cache/structs/<StructName>.json` |
| 2 | **Project sources** | Full scan: `Src/**/*.uc` containing `struct <StructName>` |
| 3 | **Dependent mod sources** | Full scan in each mod path from `build.ps1` |
| 4 | **Community Highlander** | Full scan in known Community Highlander paths |
| 5 | **Alien Highlander** | Full scan in known Alien Highlander paths |
| 6 | **SDK sources** | Full scan in SDK `SrcOrig/` |

**Step 2.3 — Parse Struct Definition:**
Extract all fields from the struct:

```unrealscript
struct SDLReplacement
{
    var XComSpawnList[] List;        // ← Nested struct array
    var bool Duplicates;              // ← Primitive
    var SDLGroup[] Groups;            // ← Nested struct array
    var int CommonWeightMultiplier;   // ← Primitive
};
```

**Step 2.4 — Recursive Nested Struct Resolution:**
For each field with a struct type:
1. Identify nested struct types (e.g., `XComSpawnList`, `SDLGroup`)
2. Recursively resolve each nested struct (repeat Step 2.1–2.4)
3. Continue until all nested structs are resolved or max depth reached (10 levels)

**Step 2.5 — Cache the Result:**
Write the resolved struct definition to `.xcom2cache/structs/<StructName>.json`:

```json
{
  "structName": "SDLReplacement",
  "sourceFile": "Src/EncounterListProcessor/Classes/ELPSDLProcessor.uc",
  "sourceHash": "sha256:abc123...",
  "lastIndexed": "2026-03-22T10:30:00Z",
  "fields": [
    { "name": "List", "type": "XComSpawnList[]", "isStruct": true, "baseType": "XComSpawnList" },
    { "name": "Duplicates", "type": "bool", "isStruct": false },
    { "name": "Groups", "type": "SDLGroup[]", "isStruct": true, "baseType": "SDLGroup" },
    { "name": "CommonWeightMultiplier", "type": "int", "isStruct": false }
  ],
  "nestedStructs": ["XComSpawnList", "SDLGroup"],
  "resolvedNestedStructs": true
}
```

**Output:** Complete struct definition with all nested structs resolved

---

#### Validation Behavior

Once the struct definition is found:

1. Compare each field name in the INI struct value against the struct's field names
2. For each unrecognized field name, report an `InvalidStructMember` error
3. If the struct definition cannot be found after all resolution steps, emit a `StructDefNotFound` warning and skip validation

**Span Coverage:** For `InvalidStructMember` and `StructDefNotFound` diagnostics, the span covers the entire KVP value portion — from the `=` character to the end of the value (including any multi-line continuations).

#### Error Output Format

**`InvalidStructMember` error:**
```
Config/XComELP.ini(7,1-13,1): InvalidStructMember: Unknown struct member "Group" in
  [EncounterListProcessor.ELPSDLProcessor] property "SDLReplacements".
  Valid members: List, Duplicates, Groups, CommonWeightMultiplier, CommonMinOffset,
    CommonMaxOffset, Extras, Purge
  Struct defined in: Src/EncounterListProcessor/Classes/ELPSDLProcessor.uc
  Variable type: SDLReplacement[]
```

**`StructDefNotFound` warning:**
```
Config/XComELP.ini(7,1-13,1): StructDefNotFound: Could not resolve struct definition
  for [SomePackage.SomeClass] property "SomeProperty" (type: UnknownStructType).
  Struct member validation skipped.
  Searched: .xcom2cache/, Src/, .scripts/build.ps1 sources, Community Highlander,
    Alien Highlander, <sdkroot>/Development/SrcOrig/
```

#### CLI Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--project-root` | `-P` | Path to the mod project root (for resolving `.scripts/build.ps1` and `Src/`) | Auto-detect from input path |
| `--no-struct-validation` | | Disable struct member validation entirely | false |
| `--force-reindex` | | Force re-indexing of all structs (ignore cache) | false |
| `--cache-dir` | | Path to struct cache directory | `.xcom2cache/structs/` |

---

## 5. Output Specification

### 5.1 CLI Output Format

```
<filepath>(<line>,<col>-<eline>,<ecol>): <error_code>: <message>
  <source_line_excerpt>
```

**Example:**
```
XComGame\Config\DefaultGame.ini(45,1-45,20): InvalidIdent: Invalid identifier
  +AlsoInvalid{01}=1
```

### 5.2 Output Modes

| Mode | Flag | Description |
|------|------|-------------|
| Default | (none) | Human-readable with source excerpts |
| JSON | `--json` | Machine-readable JSON array |
| Quiet | `--quiet` | Only print errors, no summary |
| Summary | `--summary` | Only print file counts and error totals |

### 5.3 JSON Output Schema

```json
{
  "files": [
    {
      "path": "string",
      "errors": [
        {
          "code": "string",
          "message": "string",
          "line": "number",
          "column": "number",
          "endLine": "number",
          "endColumn": "number",
          "sourceLine": "string"
        }
      ]
    }
  ],
  "summary": {
    "filesProcessed": "number",
    "filesWithErrors": "number",
    "totalErrors": "number"
  }
}
```

### 5.4 Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success (no errors; warnings do not affect exit code) |
| 1 | Validation errors found (warnings alone return 0) |
| 2 | I/O error (file not found, permission denied) |
| 3 | Invalid UTF-8 encoding |
| 4 | Invalid arguments |

**Note:** `StructDefNotFound` is a warning and does not cause exit code 1. `InvalidStructMember` is an error and does cause exit code 1.

---

## 6. CLI Interface

### 6.1 Command Syntax

```
ue3-config-parser [OPTIONS] <PATH>
```

### 6.2 Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `<PATH>` | Yes | File or directory to process |

### 6.3 Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--json` | `-j` | Output in JSON format | false |
| `--quiet` | `-q` | Suppress non-error output | false |
| `--summary` | `-s` | Show only summary | false |
| `--recursive` | `-r` | Process directories recursively | true |
| `--no-recursive` | | Disable recursive processing | - |
| `--pattern` | `-p` | Glob pattern for files | `*.ini` |
| `--project-root` | `-P` | Path to mod project root (for struct validation source resolution) | Auto-detect |
| `--no-struct-validation` | | Disable struct member validation | false |
| `--help` | `-h` | Show help message | - |
| `--version` | `-v` | Show version | - |

### 6.4 Usage Examples

```bash
# Single file
ue3-config-parser XComGame.ini

# Directory (recursive)
ue3-config-parser XComGame/Config/

# Specific pattern
ue3-config-parser --pattern "*.uc" Mods/

# JSON output for CI
ue3-config-parser --json Config/ > errors.json

# Quiet mode, exit code only
ue3-config-parser --quiet Config/ && echo "OK" || echo "ERRORS"
```

---

## 7. User Stories

### 7.1 Modder Workflow

**As a** game modder  
**I want to** validate my config changes before launching the game  
**So that** I don't crash the game due to syntax errors  

**Acceptance Criteria:**
- Run tool on modified .ini file
- See all errors with line numbers
- Fix errors iteratively
- Exit code 0 when file is valid

### 7.2 CI/CD Integration

**As a** developer  
**I want** machine-readable output  
**So that** I can integrate validation into my build pipeline  

**Acceptance Criteria:**
- `--json` flag produces valid JSON
- Exit code 1 on any error
- Can parse JSON to extract file/error info

### 7.3 Bulk Validation

**As a** QA tester  
**I want to** validate all config files in a mod folder  
**So that** I can catch errors across multiple files  

**Acceptance Criteria:**
- Recursive directory processing
- Summary of files processed and errors found
- Continue processing even if some files have errors

### 7.4 Struct Member Validation

**As a** game modder  
**I want** the parser to catch invalid struct field names before I launch the game  
**So that** I avoid runtime Redscreen errors caused by typos in struct members  

**Acceptance Criteria:**
- Parser detects `Group` (singular) when the struct defines `Groups` (plural)
- Error message shows the invalid field name, valid alternatives, and the source file where the struct is defined
- If the struct definition cannot be resolved, a warning is emitted instead of silently skipping
- Validation can be disabled with `--no-struct-validation`

---

## 8. Edge Cases

### 8.1 Empty Files
- Empty file = valid (no errors)
- File with only whitespace = valid (no errors)
- File with only comments = valid (no errors)

### 8.2 Encoding
- UTF-8 BOM = strip and process
- Invalid UTF-8 = error code 3, message "Invalid UTF-8 encoding"
- ASCII = valid subset of UTF-8

### 8.3 Large Files
- Files >100MB: process in streaming mode if possible
- Report progress for large files (optional)

### 8.4 Symbolic Links
- Follow symlinks to files
- Do not follow symlinks to directories (prevent infinite loops)

### 8.5 Permission Errors
- Log error to stderr
- Continue processing other files
- Include in summary as "skipped"

---

## 9. Out of Scope

| Feature | Reason |
|---------|--------|
| Config file merging | Beyond validation scope |
| Full semantic validation | Only struct member names are validated; game logic correctness (e.g., valid enum values, cross-reference integrity) is out of scope |
| Auto-fix suggestions | Future enhancement |
| GUI interface | CLI-first; GUI optional future |
| Non-UE3 INI files | Different grammar |
| Write operations | Read-only validation |

---

## 10. Success Metrics

| Metric | Target |
|--------|--------|
| Parse accuracy | 100% match with reference Rust implementation |
| False positive rate | <1% on real-world configs |
| False negative rate | 0% (must catch all errors) |
| CLI response time | <1s for typical config files |

---

## 11. Reference Implementation

The Rust implementation at `ue3-config-parser/` serves as the reference:
- All test cases must produce identical results
- Error messages should match semantically (exact wording can vary)
- Span/position reporting must be byte-accurate

---

## 12. Appendix: Sample Config Files

### 12.1 Valid Config
```ini
[Engine.Engine]
bSmoothFrameRate=True
MaxFPS=60

[XComGame.XComGameEngine]
+SpawnDistributionLists=(ListID="DefaultLeaders", \
    SpawnDistribution[0]=(Template="AdvWraithM1", MinForceLevel=3, MaxForceLevel=7))

; This is a valid comment
-OldProperty=SomeValue
```

### 12.2 Invalid Config (with errors)
```ini
[MyPackage.MyClass]  ; Error: trailing space after ]
+MyArray=(Abc[0]="Def", \\
    ) \\  ; Error: space after backslash
// Invalid comment style  ; Error: SlashSlashComment
+AlsoInvalid{01}=1  ; Error: InvalidIdent
```
