# XCom2ModCompiler

**XCOM 2 Mod Compiler** - A modern C#-based build system for XCOM 2 mods.

---

## 📋 Overview

XCom2ModCompiler is a complete reimplementation of the XCOM 2 mod build system in C#, providing:

- ✅ **Faster builds** through optimized file operations and parallel processing
- ✅ **Better error messages** with precise path translation and colored output
- ✅ **Incremental builds** with intelligent change detection
- ✅ **Full compatibility** with existing VS Code workflows (Ctrl+Alt+B)
- ✅ **Modern C# features** including async/await, pattern matching, and nullable reference types
- ✅ **Comprehensive testing** with unit tests and integration tests

---

## 🚀 Quick Start

### Prerequisites

- **.NET 8.0 Runtime** or later
- **XCOM 2 War of the Chosen SDK** installed
- **XCOM 2 WOTC** game installed (Steam)
- **PowerShell 5.1** or later
- **VS Code** with C# extension (recommended)

### Installation

1. **Clone or download** this repository to your mod project:
   ```bash
   # Copy XCom2ModCompiler to your mod's .scripts directory
   cp -r XCom2ModCompiler /path/to/YourMod/.scripts/
   ```

2. **Update your `build.ps1`** to use the C# compiler:
   ```powershell
   # See .scripts/build.ps1 for example
   ```

3. **Build your mod** in VS Code:
   - Press `Ctrl+Alt+B` for default build
   - Press `Ctrl+Shift+B` to select build task

### Configuration

Configure build paths in VS Code settings (`.vscode/settings.json`):

```json
{
    "xcom.highlander.sdkroot": "D:\\Games\\XCOM 2 War of the Chosen SDK",
    "xcom.highlander.gameroot": "D:\\Games\\XCOM 2\\XCom2-WaroftheChosen",
    "xcom.highlander.moddestination": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\XCOM 2\\XCom2-WarOfTheChosen\\XComGame\\Mods"
}
```

---

## 📖 Documentation

| Document | Description |
|----------|-------------|
| [System Design Document](docs/01_System_Design_Document.md) | High-level system overview, goals, scope |
| [Technical Design Document](docs/02_Technical_Design_Document.md) | Component design, data models, algorithms |
| [Architecture Document](docs/03_Architecture_Document.md) | Architecture patterns, quality requirements |

---

## 🛠️ Usage

### Command Line Interface

```bash
# Build a mod
XCom2ModCompiler build \
  --mod-name "MyMod" \
  --src-directory "D:\Projects\MyMod" \
  --sdk-path "D:\Games\XCOM 2 War of the Chosen SDK" \
  --game-path "D:\Games\XCOM 2\XCom2-WaroftheChosen" \
  --config default

# Clean build artifacts
XCom2ModCompiler clean \
  --mod-name "MyMod" \
  --src-directory "D:\Projects\MyMod" \
  --sdk-path "D:\Games\XCOM 2 War of the Chosen SDK"

# Build with debug symbols
XCom2ModCompiler build --config debug

# Build Highlander with final release
XCom2ModCompiler build --final-release
```

### PowerShell Wrapper

```powershell
# Default build
.\.scripts\build.ps1 `
  -srcDirectory "D:\Projects\MyMod" `
  -sdkPath "D:\Games\XCOM 2 War of the Chosen SDK" `
  -gamePath "D:\Games\XCOM 2\XCom2-WaroftheChosen" `
  -config default

# Debug build
.\.scripts\build.ps1 `
  -srcDirectory "D:\Projects\MyMod" `
  -sdkPath "D:\Games\XCOM 2 War of the Chosen SDK" `
  -gamePath "D:\Games\XCOM 2\XCom2-WaroftheChosen" `
  -config debug
```

### Build Options

| Option | Description | Values |
|--------|-------------|--------|
| `--mod-name` | Name of your mod | String |
| `--src-directory` | Path to mod project root | Path |
| `--sdk-path` | Path to XCOM 2 SDK installation | Path |
| `--game-path` | Path to XCOM 2 game installation | Path |
| `--config` | Build configuration | `default`, `debug` |
| `--final-release` | Enable final release mode (Highlander only) | Flag |
| `--workshop-id` | Override Steam Workshop ID | Number |
| `--include-src` | Add dependency source path | Path (multiple) |
| `--clean-mod` | Add mod to clean before build | String (multiple) |
| `--content-options` | Path to ContentOptions.json | Path |
| `--verbose` | Enable verbose logging | Flag |
| `--timing-report` | Show build timing breakdown | Flag |

---

## 📁 Project Structure

```
XCom2ModCompiler/
├── XCom2ModCompiler.csproj    # Project file
├── Program.cs                 # CLI entry point
├── BuildController.cs         # Main build orchestrator
├── Configuration/             # Build configuration classes
│   ├── BuildOptions.cs
│   ├── ContentOptions.cs
│   └── UserConfig.cs
├── Compilation/               # Script compilation
│   ├── ScriptCompiler.cs
│   ├── MacroValidator.cs
│   └── OutputReceivers.cs
├── Cooking/                   # Asset cooking
│   ├── AssetCooker.cs
│   ├── TfcManager.cs
│   └── CookerOutputTracker.cs
├── Deployment/                # File deployment
│   ├── FileMirror.cs
│   ├── StagingManager.cs
│   └── ModMetadata.cs
├── Tracking/                  # Build tracking
│   ├── BuildTracker.cs
│   └── TimestampTracker.cs
├── Utilities/                 # Helper utilities
│   ├── ProcessExtensions.cs
│   ├── PathUtilities.cs
│   └── TableFormatter.cs
├── Exceptions/                # Custom exceptions
│   ├── BuildException.cs
│   ├── BuildCrashException.cs
│   └── BuildFailureException.cs
└── docs/                      # Documentation
    ├── 01_System_Design_Document.md
    ├── 02_Technical_Design_Document.md
    └── 03_Architecture_Document.md
```

---

## 🔧 Development

### Building from Source

```bash
# Clone the repository
git clone https://github.com/your-org/XCom2ModCompiler.git

# Navigate to project directory
cd XCom2ModCompiler

# Build the project
dotnet build

# Run tests
dotnet test

# Run CLI (example)
dotnet run -- build --help
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test category
dotnet test --filter "Category=Unit"
```

### Code Style

This project follows .NET conventions with:
- Nullable reference types enabled
- Async/await pattern throughout
- Dependency injection for testability
- XML documentation for public APIs

---

## 🤝 Contributing

Contributions are welcome! Please:

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Open** a Pull Request

### Development Guidelines

- Write unit tests for new features
- Maintain or improve code coverage
- Update documentation for API changes
- Follow existing code style
- Use meaningful commit messages

---

## 🐛 Troubleshooting

### Common Issues

#### Build fails with "Path not found"

**Solution:** Verify SDK and game paths in VS Code settings are correct.

#### Build is slow

**Solution:** 
- Enable incremental builds (don't clean unnecessarily)
- Exclude large directories from antivirus scanning
- Use SSD for SDK and game installations

#### PowerShell execution policy error

**Solution:** Run PowerShell as administrator and execute:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

#### XComGame.com crashes

**Solution:**
- Verify SDK installation is complete
- Check for mod conflicts in Src directory
- Review build logs for specific error messages

### Getting Help

- **Documentation:** See `docs/` directory
- **Issues:** Open an issue on GitHub
- **Discussions:** GitHub Discussions tab

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- **X2ModBuildCommon** - Original PowerShell build system
- **XCOM 2 Modding Community** - Tools and documentation
- **Firaxis Games** - XCOM 2 and modding support

---

## 📊 Roadmap

### Version 1.0 (Current)
- [x] Core build orchestration
- [x] Script compilation
- [x] Asset cooking
- [x] File deployment
- [x] PowerShell wrapper
- [x] Documentation

### Version 1.1 (Planned)
- [ ] Parallel file operations
- [ ] Enhanced error messages
- [ ] Build caching improvements
- [ ] CI/CD integration

### Version 2.0 (Future)
- [ ] Real-time build monitoring
- [ ] Visual Studio extension
- [ ] Dependency management
- [ ] Workshop publishing automation

---

## 📞 Contact

- **GitHub:** [@your-org](https://github.com/your-org)
- **Discord:** XCOM 2 Modding Discord
- **Reddit:** r/XCOM

---

*Built with ❤️ for the XCOM 2 modding community*
