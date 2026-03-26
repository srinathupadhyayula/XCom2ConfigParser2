# XCom2ConfigParser2 Settings Schema

This document describes the `.vscode/settings.json` configuration options for the UE3 Config Parser & Validator.

## Configuration Keys

All configuration keys are prefixed with `xcom.configParser.` for clarity and to avoid conflicts with other extensions.

### `xcom.configParser.iniRoots`
**Type:** `string[]` (array of paths)  
**Description:** Directories containing `.ini` configuration files to validate.  
**Default:** `["Config"]` (relative to project root)

```json
"xcom.configParser.iniRoots": [
    "Config",
    "XComGame/Config",
    "../Common/Config"
]
```

### `xcom.configParser.localSrcRoot`
**Type:** `string` (path)  
**Description:** Path to the local project's `Src/` directory containing UnrealScript class files.  
**Default:** `"Src"` (relative to project root)

```json
"xcom.configParser.localSrcRoot": "Src"
```

### `xcom.configParser.buildScriptPath`
**Type:** `string` (path)  
**Description:** Path to the build script (`build.ps1`) that defines mod dependencies via `$builder.IncludeSrc()`.  
**Default:** `".scripts/build.ps1"` (relative to project root)

```json
"xcom.configParser.buildScriptPath": ".scripts/build.ps1"
```

### `xcom.configParser.modsCompiledAgainst`
**Type:** `string[]` (array of paths)  
**Description:** Explicit list of mod source directories this project compiles against. If not specified, parsed from `buildScriptPath`.  
**Default:** `[]` (parsed from build script)

```json
"xcom.configParser.modsCompiledAgainst": [
    "../CommunityHighlander/Src",
    "../AlienHighlander/Src",
    "../EncounterListProcessor/Src"
]
```

### `xcom.configParser.communityHighlanderPath`
**Type:** `string` (path)  
**Description:** Path to the Community Highlander mod's `Src/` directory.  
**Default:** Not set (must be specified if using Community Highlander)

```json
"xcom.configParser.communityHighlanderPath": "~/.xcom2/mods/CommunityHighlander/Src"
```

### `xcom.configParser.alienHighlanderPath`
**Type:** `string` (path)  
**Description:** Path to the Alien Highlander mod's `Src/` directory.  
**Default:** Not set (must be specified if using Alien Highlander)

```json
"xcom.configParser.alienHighlanderPath": "~/.xcom2/mods/AlienHighlander/Src"
```

### `xcom.configParser.allModsRoot`
**Type:** `string` (path)  
**Description:** Root directory containing all other mod projects. Searched as last resort for struct definitions.  
**Default:** `../../Mods/` (relative to project root)

```json
"xcom.configParser.allModsRoot": "../../Mods/"
```

### `xcom.configParser.cachePath`
**Type:** `string` (path)  
**Description:** Directory for storing struct definition cache mappings. Cache avoids re-parsing struct definitions.  
**Default:** `.xcom2cache/structs/` (relative to project root)

```json
"xcom.configParser.cachePath": ".xcom2cache/structs/"
```

### `xcom.highlander.sdkroot`
**Type:** `string` (path)  
**Description:** Path to the XCOM 2 SDK source directory. Used as final fallback for struct resolution.  
**Default:** Not set (must be specified)

```json
"xcom.highlander.sdkroot": "C:/Program Files (x86)/Steam/steamapps/common/XCOM 2 War of the Chosen/XCom2-WarOfTheChosen"
```

## Complete Example

```json
{
    "xcom.highlander.sdkroot": "C:/Program Files (x86)/Steam/steamapps/common/XCOM 2 War of the Chosen/XCom2-WarOfTheChosen",
    
    "xcom.configParser.iniRoots": [
        "Config",
        "XComGame/Config"
    ],
    
    "xcom.configParser.localSrcRoot": "Src",
    
    "xcom.configParser.buildScriptPath": ".scripts/build.ps1",
    
    "xcom.configParser.modsCompiledAgainst": [
        "../CommunityHighlander/Src",
        "../AlienHighlander/Src"
    ],
    
    "xcom.configParser.communityHighlanderPath": "~/.xcom2/mods/CommunityHighlander/Src",
    "xcom.configParser.alienHighlanderPath": "~/.xcom2/mods/AlienHighlander/Src",
    
    "xcom.configParser.allModsRoot": "../../Mods/",
    
    "xcom.configParser.cachePath": ".xcom2cache/structs/"
}
```

## Search Order for Struct Resolution

When resolving struct definitions, the parser searches in the following order:

1. **Local Cache** - `.xcom2cache/structs/<StructName>.json`
2. **Local Src** - `xcom.configParser.localSrcRoot` (full scan)
3. **Mods Compiled Against** - `xcom.configParser.modsCompiledAgainst` (each path, full scan)
4. **SDK Sources** - `xcom.highlander.sdkroot/Development/SrcOrig` (full scan)
5. **Community Highlander** - `xcom.configParser.communityHighlanderPath` (only if not already in mods compiled against)
6. **Alien Highlander** - `xcom.configParser.alienHighlanderPath` (only if not already in mods compiled against)
7. **All Mods** - `xcom.configParser.allModsRoot` (full scan, **excludes** paths already searched in steps 2-6)

**Search Strategy:**
- **Mods compiled against** are searched early since your mod directly depends on them
- **SDK sources** are searched before Highlander mods because they contain the base game definitions
- **Highlander mods** are skipped if already included in "mods compiled against" to avoid duplicate searches
- **All Mods** is the last resort and automatically excludes all previously searched directories for better performance

## Path Resolution

All relative paths are resolved relative to the **project root**, which is determined by:
1. The directory containing the `.vscode/settings.json` file, OR
2. The `--project-root` CLI argument, OR
3. Auto-detected from the input file/directory path

Special path tokens:
- `~` - User home directory (`%USERPROFILE%` on Windows, `$HOME` on Unix)
- `${workspaceFolder}` - Project root directory
