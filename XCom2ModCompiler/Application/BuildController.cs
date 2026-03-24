using Microsoft.Extensions.Logging;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Compilation;
using XCom2ModCompiler.Cooking;
using XCom2ModCompiler.Tracking;
using XCom2ModCompiler.Utilities;
using XCom2ModCompiler.Exceptions;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using ZLogger;
using Kokuban;

namespace XCom2ModCompiler.Application;

public class BuildController
{
    private readonly BuildOptions _options;
    private readonly ILogger<BuildController> _logger;
    private readonly BuildTracker _tracker;
    private readonly ScriptCompiler _compiler;
    private readonly AssetCooker _cooker;
    private readonly IFileMirrorParity _mirror;
    private readonly IProcessRunner _runner;
    private readonly ShaderPrecompiler _shaderPrecompiler;
    private readonly MissingUncookedCopier _missingUncookedCopier;
    private readonly ProjectSynchronizer _projectSynchronizer;

    // Native script packages (same as PowerShell build_common.ps1)
    private static readonly string[] _nativeScriptPackages = new[]
    {
        "XComGame", "Core", "Engine", "GFxUI", "AkAudio", "GameFramework",
        "UnrealEd", "GFxUIEditor", "IpDrv", "OnlineSubsystemPC",
        "OnlineSubsystemLive", "OnlineSubsystemSteamworks", "OnlineSubsystemPSN"
    };

    /// <summary>
    /// Logs a specific header when Phase 1 fails as expected due to missing dependencies.
    /// This failure is common in Two-Pass mods during the first pass and is recovered in Phase 2.
    /// </summary>
    /// <param name="compileTarget">The mod package being compiled.</param>
    private void LogPhase1ExpectedFailure(string compileTarget)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("################################################################################");
        Console.WriteLine($"# [PHASE 1] EXPECTED dependency failure in: {compileTarget}");
        Console.WriteLine($"# [PHASE 1] Binary '{_options.ModNameCanonical}.u' was created successfully.");
        Console.WriteLine("# Proceeding to Phase 2 for final linkage of dependent packages...");
        Console.WriteLine("################################################################################");
        Console.ForegroundColor = originalColor;
    }
    // Decorator constants
    private const string Separator = "========================================";
    private const string ThinSeparator = "----------------------------------------";

    public BuildController(BuildOptions options, ILogger<BuildController> logger, BuildTracker tracker, ScriptCompiler compiler, AssetCooker cooker, IFileMirrorParity mirror, IProcessRunner runner, ShaderPrecompiler shaderPrecompiler, MissingUncookedCopier missingUncookedCopier, ProjectSynchronizer projectSynchronizer)
    {
        _options = options;
        _logger = logger;
        _tracker = tracker;
        _compiler = compiler;
        _cooker = cooker;
        _mirror = mirror;
        _runner = runner;
        _shaderPrecompiler = shaderPrecompiler;
        _missingUncookedCopier = missingUncookedCopier;
        _projectSynchronizer = projectSynchronizer;
    }

    /// <summary>
    /// Prints a decorated header with separator lines.
    /// </summary>
    private static void PrintHeader(string message, ConsoleColor color = ConsoleColor.White)
    {
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.ForegroundColor = color;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
        Console.WriteLine(Separator);
        Console.WriteLine();
    }

    /// <summary>
    /// Prints a success header in green.
    /// </summary>
    private static void PrintSuccessHeader(string message)
    {
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
        Console.WriteLine(Separator);
        Console.WriteLine();
    }

    /// <summary>
    /// Prints an error header in red.
    /// </summary>
    private static void PrintErrorHeader(string message)
    {
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
        Console.WriteLine(Separator);
        Console.WriteLine();
    }

    /// <summary>
    /// Prints an info header in cyan.
    /// </summary>
    private void PrintInfoHeader(string title, ConsoleColor color = ConsoleColor.White)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine("========================================");
        Console.WriteLine($"  {title}");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.ForegroundColor = originalColor;
    }

    /// <summary>
    /// Prints a warning header in yellow.
    /// </summary>
    private static void PrintWarningHeader(string message)
    {
        Console.WriteLine();
        Console.WriteLine(Separator);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
        Console.WriteLine(Separator);
        Console.WriteLine();
    }

    public void EnableDebug()
    {
        _options.Debug = true;
        _CheckFlags();
    }

    public void EnableFinalRelease()
    {
        _options.FinalRelease = true;
        _CheckFlags();
    }

    /// <summary>
    /// Validates that debug and final_release flags are not both enabled.
    /// Mimics PowerShell _CheckFlags() method.
    /// </summary>
    private void _CheckFlags()
    {
        if (_options.Debug && _options.FinalRelease)
        {
            throw new BuildConfigurationException("Flags", "-debug and -final_release cannot be used together");
        }
    }

    public void SetWorkshopId(long id) => _options.WorkshopId = id;
    public void IncludeSrc(string path) => _options.IncludePaths.Add(path);
    public void AddToClean(string modName) => _options.CleanMods.Add(modName);
    public void SetContentOptionsJson(string filename) => _options.ContentOptionsJson = filename;

    /// <summary>
    /// Adds a pre-make hook to be executed before script compilation.
    /// Mimics PowerShell AddPreMakeHook() method.
    /// </summary>
    public void AddPreMakeHook(Action hook)
    {
        _options.PreMakeHooks.Add(hook);
    }

    /// <summary>
    /// Executes a build step with progress reporting and timing.
    /// </summary>
    private async Task<T> PerformStepAsync<T>(
        Func<Task<T>> step,
        string progressWord,
        string completedWord,
        string description,
        List<TimingRecord> timings)
    {
        Console.WriteLine($"{Chalk.Cyan[progressWord]} {Chalk.White[description]}...");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await step();
            stopwatch.Stop();

            Console.WriteLine($"{Chalk.Green[completedWord]} {Chalk.White[description]} in {Chalk.Gray[stopwatch.Elapsed.TotalSeconds.ToString("F6")]}s");

            timings.Add(new TimingRecord(
                $"{progressWord} {description}",
                stopwatch.Elapsed.TotalSeconds,
                ""));

            return result;
        }
        catch
        {
            stopwatch.Stop();
            Console.WriteLine($"{Chalk.Red[progressWord]} {Chalk.White[description]} {Chalk.Red["FAILED"]} after {Chalk.Gray[stopwatch.Elapsed.TotalSeconds.ToString("F6")]}s");
            throw;
        }
    }

    /// <summary>
    /// Executes a build step with progress reporting and timing (void return).
    /// </summary>
    private async Task PerformStepAsync(
        Func<Task> step,
        string progressWord,
        string completedWord,
        string description,
        List<TimingRecord> timings)
    {
        await PerformStepAsync(async () =>
        {
            await step();
            return true;
        }, progressWord, completedWord, description, timings);
    }

    /// <summary>
    /// Executes a synchronous build step with progress reporting and timing.
    /// </summary>
    private void PerformStep(
        Action step,
        string progressWord,
        string completedWord,
        string description,
        List<TimingRecord> timings)
    {
        _logger.LogInformation($"{progressWord} {description}...");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            step();
            stopwatch.Stop();
            
            _logger.LogInformation($"{completedWord} {description} in {stopwatch.Elapsed.TotalSeconds:F6}s");
            
            timings.Add(new TimingRecord(
                $"{progressWord} {description}",
                stopwatch.Elapsed.TotalSeconds,
                ""));
        }
        catch
        {
            stopwatch.Stop();
            Console.WriteLine(Chalk.Red[$"{progressWord} {description} FAILED after {stopwatch.Elapsed.TotalSeconds:F6}s"]);
            throw;
        }
    }

    public async Task<BuildResult> InvokeBuildAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var timings = new List<TimingRecord>();
        var outputPaths = new List<string>();

        try
        {
            // Print build start header
            PrintInfoHeader($"BUILDING {_options.ModName}");

            // Echo paths at start (mimics PowerShell _ConfirmPaths)
            Console.WriteLine(Chalk.Gray[$"SDK Path: {_options.SdkPath}"]);
            Console.WriteLine(Chalk.Gray[$"Game Path: {_options.GamePath}"]);
            Console.WriteLine();
            var configStr = _options.Debug ? "DEBUG" : (_options.FinalRelease ? "FINAL RELEASE" : "RELEASE");
            Console.WriteLine(Chalk.Cyan[$"Configuration: {configStr}"]);
            Console.WriteLine();

            ValidateConfiguration();

            // 0. Synchronize Project (ItemGroup regeneration)
            PrintInfoHeader("STEP 1: REGENERATING ITEMGROUP");
            var x2projPath = Path.Combine(_options.ModSrcRoot, $"{_options.ModName}.x2proj");
            PerformStep(() => _projectSynchronizer.Synchronize(x2projPath), "Regenerating", "Regenerated", "ItemGroup in .x2proj file", timings);

            // Clean additional mods if specified (must be before copying mod to staging)
            if (_options.CleanMods.Count > 0)
            {
                PrintInfoHeader("STEP 2: CLEANING ADDITIONAL MODS");
                await PerformStepAsync(
                    async () =>
                    {
                        foreach (var modName in _options.CleanMods)
                        {
                            var cleanDir = Path.Combine(_options.SdkPath, "XComGame", "Mods", modName);
                            if (Directory.Exists(cleanDir))
                            {
                                Console.WriteLine(Chalk.Yellow[$"Cleaning {modName}..."]);
                                await _mirror.DeleteAsync(cleanDir, recursive: true, ct: ct);
                            }
                        }
                    },
                    "Cleaning", "Cleaned", "additional mods", timings);
            }

            var projectDir = FindProjectDirectory(_options.ModSrcRoot);
            var modSrcPath = Path.Combine(projectDir, "Src");

            // 1. Preparation: Copy mod to staging area
            PrintInfoHeader("STEP 3: MIRRORING MOD TO STAGING");
            await PerformStepAsync(
                async () =>
                {
                    _logger.LogInformation($"Mirroring {_options.ModSrcRoot} to {_options.StagingPath}");
                    await _mirror.MirrorAsync(_options.ModSrcRoot, _options.StagingPath, "*.*",
                        new[] { "*.x2proj", _options.ContentOptionsJson }, new[] { "ContentForCook" }, ct: ct);

                    // Create Script and CookedPCConsole folders (PS1 parity lines 464, 487)
                    Directory.CreateDirectory(Path.Combine(_options.StagingPath, "Script"));
                    Directory.CreateDirectory(Path.Combine(_options.StagingPath, "CookedPCConsole"));
                },
                "Mirroring", "Mirrored", "mod to staging", timings);

            // 1.5. Copy ALL Src folder contents to SDK Development\Src (Non-destructively, PS1 parity)
            await PerformStepAsync(
                async () =>
                {
                    var sdkDevSrcPath = Path.Combine(_options.SdkPath, "Development", "Src");
                    var sdkDevSrcOrigPath = Path.Combine(_options.SdkPath, "Development", "SrcOrig");

                    // 1.5a Mirror SrcOrig to Src (Clean slate, PS1 parity)
                    if (Directory.Exists(sdkDevSrcOrigPath))
                    {
                        _logger.LogInformation($"Mirroring {sdkDevSrcOrigPath} to {sdkDevSrcPath}");
                        await _mirror.MirrorAsync(sdkDevSrcOrigPath, sdkDevSrcPath, "*.uc *.uci", ct: ct);
                    }

                    // Parse macros for redefinition tracking (PS1 parity)
                    var definedMacros = new Dictionary<string, (string Path, int Line)>(StringComparer.OrdinalIgnoreCase);
                    var targetGlobalsFile = Path.Combine(sdkDevSrcPath, "Core", "Globals.uci");

                    if (File.Exists(targetGlobalsFile))
                    {
                        ParseMacroFile(targetGlobalsFile, definedMacros);
                    }

                    // 1.5b Mirror all include paths (Dependencies)
                    foreach (var includePath in _options.IncludePaths)
                    {
                        await CopySrcFolderAsync(includePath, sdkDevSrcPath, definedMacros, ct);
                    }

                    // 1.5c Mirror main mod sources
                    if (Directory.Exists(modSrcPath))
                    {
                        await CopySrcFolderAsync(modSrcPath, sdkDevSrcPath, definedMacros, ct);
                    }
                    else
                    {
                        // Fallback discovery: some mods put packages directly in the project root
                        _logger.LogInformation($"No 'Src' folder found in {projectDir}. Falling back to root source discovery.");
                        await CopySrcFolderAsync(projectDir, sdkDevSrcPath, definedMacros, ct);
                    }
                },
                "Populating", "Populated", "Development\\Src folder", timings);

            // Generate .XComMod file
            await PerformStepAsync(
                () => GenerateXComModFileAsync(_options.StagingPath, ct),
                "Generating", "Generated", "mod metadata (.XComMod file)", timings);

            // 2. Convert localization files in STAGING area (PS1 parity)
            await PerformStepAsync(
                () =>
                {
                    _logger.LogInformation($"Converting localization in {_options.StagingPath}");
                    LocalizationConverter.Convert(_options.StagingPath);
                    return Task.CompletedTask;
                },
                "Converting", "Converted", "Localization UTF-8 -> UTF-16", timings);

            // Load content options
            Console.WriteLine("Preparing content options");
            var contentOptions = new ContentOptions();
            if (!string.IsNullOrEmpty(_options.ContentOptionsJson) && File.Exists(Path.Combine(_options.ModSrcRoot, _options.ContentOptionsJson)))
            {
                var json = File.ReadAllText(Path.Combine(_options.ModSrcRoot, _options.ContentOptionsJson));
                contentOptions = System.Text.Json.JsonSerializer.Deserialize<ContentOptions>(json) ?? new ContentOptions();
                Console.WriteLine($"Loaded {_options.ContentOptionsJson}");
            }
            else
            {
                Console.WriteLine("No content options specified");
            }

            // Log what content options are NOT present (mimics PowerShell detailed logging)
            if (contentOptions.MissingUncooked.Count == 0)
            {
                Console.WriteLine("No missing uncooked");
            }
            if (contentOptions.SfStandalone.Count == 0)
            {
                Console.WriteLine("No packages to make SF");
            }
            if (contentOptions.SfMaps.Count == 0)
            {
                Console.WriteLine("No umaps to cook");
            }
            if (contentOptions.SfCollectionMaps.Count == 0)
            {
                Console.WriteLine("No collection maps to cook");
            }

            // Check if selective clean is needed (mimics PowerShell _CheckCleanCompiled)
            await PerformStepAsync(
                async () =>
                {
                    var corePath = Path.Combine(_options.SdkPath, "XComGame", "Script", "Core.u");
                    DateTime? coreTime = File.Exists(corePath) ? File.GetLastWriteTime(corePath) : null;
                    var globalsPath = Path.Combine(_options.SdkPath, "Development", "Src", "Core", "Globals.uci");
                    var globalsHash = BuildTracker.ComputeFileHash(globalsPath);

                    bool shouldRebuild = await _tracker.ShouldRebuildAsync(_options, globalsHash, coreTime, ct);

                    if (shouldRebuild)
                    {
                        // Get the current INI content to know what packages exist
                        string targetIni = Path.Combine(_options.SdkPath, "XComGame", "Config", "XComEngine.ini");
                        if (File.Exists(targetIni))
                        {
                            string iniContent = await File.ReadAllTextAsync(targetIni, ct);
                            var allScriptPackages = GetAllScriptPackages(iniContent);
                            
                            if (allScriptPackages.Length > 0)
                            {
                                var pathsToClean = await _tracker.GetSelectiveCleanPathsAsync(_options.SdkPath, allScriptPackages, ct);
                                var scriptPath = Path.Combine(_options.SdkPath, "XComGame", "Script");
                                Console.WriteLine($"Selective cleaning of compiled scripts from {scriptPath} to avoid compiler error...");
                                foreach (var path in pathsToClean)
                                {
                                    if (File.Exists(path))
                                    {
                                        Console.WriteLine($"Cleaning {path}");
                                        await _mirror.DeleteAsync(path, ct: ct);
                                    }
                                }
                                Console.WriteLine("Cleaned.");
                            }
                        }
                    }
                },
                "Verifying", "Verified", "compiled script packages", timings);

            // Shader precompilation
            PrintInfoHeader("STEP 7: PRECOMPILING SHADERS");
            await PerformStepAsync(() => _shaderPrecompiler.PrecompileAsync(_options, ct), "Precompiling", "Precompiled", "shaders", timings);

            // Execute pre-make hooks
            if (_options.PreMakeHooks.Count > 0)
            {
                PrintInfoHeader("STEP 8: RUNNING PRE-MAKE HOOKS");
                await PerformStepAsync(
                    () =>
                    {
                        foreach (var hook in _options.PreMakeHooks)
                        {
                            hook.Invoke();
                        }
                        return Task.CompletedTask;
                    },
                    "Running", "Ran", "pre-make hooks", timings);
            }

            // 2. Compile Script Packages (with two-pass strategy for dependent packages)
            PrintInfoHeader("STEP 9: COMPILING SCRIPT PACKAGES");
            var receiver = new MakeOutputReceiver(new[] { _options.ModSrcRoot }.Concat(_options.IncludePaths).ToArray());
            string? iniToRestore = null;
            string? targetIniPath = null;

            try
            {
                await PerformStepAsync(
                    async () =>
                    {
                        var settingsLoader = new XCom2ModCompiler.Configuration.SettingsLoader(_options.ProjectRoot);
                        var settings = settingsLoader.Load();
                        var iniHandler = new IniHandler(_options.ProjectRoot, settings.IniRoots);

                        targetIniPath = iniHandler.FindTargetIni();

                        if (!string.IsNullOrEmpty(targetIniPath) && File.Exists(targetIniPath))
                        {
                            // Read and store the original INI content for restoration
                            iniToRestore = await File.ReadAllTextAsync(targetIniPath, ct);

                            // Check if two-pass compilation is needed (i.e., if DependantPackages section exists)
                            var dependentPackages = iniHandler.GetDependantPackages(iniToRestore);
                            bool twoPassNeeded = dependentPackages.Count > 0;

                            if (twoPassNeeded)
                            {
                                Console.WriteLine(Chalk.Yellow["Two-pass compilation detected (DependantPackages section found)"]);
                                Console.WriteLine(Chalk.Gray[ThinSeparator]);
                                await ExecuteTwoPassCompilationAsync(iniHandler, targetIniPath, iniToRestore, dependentPackages, receiver, ct);
                            }
                            else
                            {
                                // Single-pass compilation (standard flow)
                                await ExecuteSinglePassCompilationAsync(iniHandler, targetIniPath, iniToRestore, receiver, ct);
                            }

                            // Record output paths for successful build reporting
                            outputPaths.Add(Path.Combine(_options.SdkPath, "XComGame", "Script", $"{_options.ModNameCanonical}.u"));
                            foreach (var depMod in dependentPackages)
                            {
                                outputPaths.Add(Path.Combine(_options.SdkPath, "XComGame", "Script", $"{depMod}.u"));
                            }
                        }
                        else
                        {
                            // Fallback if no INI found (should not happen in standard setup)
                            bool success = await _compiler.CompileModAsync(_options.ModNameCanonical, _options.StagingPath, _options, receiver, ct);
                            if (!success)
                            {
                                throw new BuildFailureException("Script compilation", 1);
                            }
                            outputPaths.Add(Path.Combine(_options.SdkPath, "XComGame", "Script", $"{_options.ModNameCanonical}.u"));
                        }
                    },
                    "Compiling", "Compiled", "script packages", timings);
            }
            finally
            {
                // ALWAYS restore INI after compilation (whether success or failure)
                if (!string.IsNullOrEmpty(iniToRestore) && !string.IsNullOrEmpty(targetIniPath))
                {
                    _logger.LogInformation("Restoring XComEngine.ini to original content...");
                    await File.WriteAllTextAsync(targetIniPath, iniToRestore, ct);
                }
            }

            // Record Core.u timestamp (needed for incremental build detection)
            PrintInfoHeader("STEP 10: RECORDING CORE.U TIMESTAMP");
            await PerformStepAsync(
                () => _tracker.RecordCoreTimestampAsync(_options.SdkPath, ct),
                "Recording", "Recorded", "Core.u timestamp", timings);

            // Copy script packages to staging
            PrintInfoHeader("STEP 11: COPYING SCRIPT PACKAGES");
            var allScriptPackages = GetModScriptPackagesFromSrc();
            if (allScriptPackages.Length > 0)
            {
                await PerformStepAsync(
                    () => CopyScriptPackagesAsync(allScriptPackages, _options.StagingPath, ct),
                    "Copying", "Copied", "compiled script packages", timings);

                // Check if this is a Highlander mod (has native packages)
                bool isHighlander = HasNativePackages(allScriptPackages);

                // Cook Highlander packages if not debug mode
                if (isHighlander && !_options.Debug)
                {
                    PrintInfoHeader("STEP 12: COOKING HIGHLANDER PACKAGES");
                    await PerformStepAsync(
                        () => CookHighlanderPackagesAsync(allScriptPackages, ct),
                        "Cooking", "Cooked", "Highlander packages", timings);
                }
                else if (isHighlander && _options.Debug)
                {
                    Console.WriteLine(Chalk.Yellow["Skipping Highlander cooking as debug build"]);
                }
            }

            // Shader precompilation (needs to happen before asset cooking)
            PrintInfoHeader("STEP 13: PRECOMPILING SHADERS (POST-COMPILE)");
            await PerformStepAsync(() => _shaderPrecompiler.PrecompileAsync(_options, ct), "Precompiling", "Precompiled", "shaders", timings);

            // 4. Cooking (if content options present)
            PrintInfoHeader("STEP 14: COOKING ASSETS");
            if (!string.IsNullOrEmpty(_options.ContentOptionsJson))
            {
                await PerformStepAsync(
                    async () =>
                    {
                        bool cookSuccess = await _cooker.CookAsync(_options.ModNameCanonical, _options.StagingPath, contentOptions, _options, ct);
                        if (!cookSuccess)
                        {
                            throw new BuildFailureException("Asset cooking", 1);
                        }
                    },
                    "Cooking", "Cooked", "mod assets", timings);
            }
            else
            {
                Console.WriteLine(Chalk.Gray["No asset cooking requested, skipping"]);
            }

            // 4.5 Copy missing uncooked
            PrintInfoHeader("STEP 15: COPYING MISSING UNCOOKED");
            await PerformStepAsync(
                () => _missingUncookedCopier.CopyMissingAsync(_options, contentOptions, ct),
                "Copying", "Copied", "missing uncooked packages", timings);

            // 5. Final Copy to Game Dir
            PrintInfoHeader("STEP 16: FINAL COPY TO GAME DIRECTORY");
            await PerformStepAsync(
                async () =>
                {
                    Console.WriteLine(Chalk.Cyan["Copying built mod to game directory..."]);
                    await _mirror.MirrorAsync(_options.StagingPath, _options.FinalModPath, ct: ct);

                    Console.WriteLine(Chalk.Cyan[$"Removing source staging directory {_options.StagingPath} after successful move to {_options.FinalModPath}..."]);
                    if (Directory.Exists(_options.StagingPath))
                    {
                        Directory.Delete(_options.StagingPath, recursive: true);
                    }
                    Console.WriteLine(Chalk.Green["Source directory removed. Move operation completed successfully."]);
                },
                "Copying", "Copied", "output to final destination", timings);

            // Clean leftover scripts from SDK/Game
            if (allScriptPackages.Length > 0)
            {
                PrintInfoHeader("STEP 17: CLEANING LEFTOVER SCRIPTS");
                await PerformStepAsync(
                    () => CleanLeftoverScriptsAsync(allScriptPackages, ct),
                    "Cleaning", "Cleaned", "leftover script packages from SDK/Game directory", timings);
            }

            // Record fingerprint
            var coreTime2 = File.GetLastWriteTime(Path.Combine(_options.SdkPath, "XComGame", "Script", "Core.u"));
            var globalsHash2 = BuildTracker.ComputeFileHash(Path.Combine(_options.SdkPath, "Development", "Src", "Core", "Globals.uci"));

            var timestamps = new Dictionary<string, DateTime>();
            var srcFiles = Directory.GetFiles(_options.ModSrcRoot, "*.*", SearchOption.AllDirectories);
            foreach (var f in srcFiles) timestamps[f] = File.GetLastWriteTimeUtc(f);

            var contentForCookPath = Path.Combine(_options.ModSrcRoot, "ContentForCook");
            if (Directory.Exists(contentForCookPath))
            {
                var contentFiles = Directory.GetFiles(contentForCookPath, "*.*", SearchOption.AllDirectories);
                foreach (var f in contentFiles) timestamps[f] = File.GetLastWriteTimeUtc(f);
            }

            await _tracker.SaveFingerprintAsync(new BuildFingerprint(
                _options.Debug ? "debug" : "release",
                globalsHash2,
                coreTime2,
                DateTime.UtcNow,
                timestamps), ct);

            Console.WriteLine();
            Console.WriteLine(Chalk.Gray[$"Build completed successfully in {sw.Elapsed.TotalSeconds:F2}s"]);

            ReportTimings(timings, sw.Elapsed);

            BuildUtilities.PlaySuccessSound();
            PrintSuccessHeader($"*** SUCCESS! ({BuildUtilities.FormatElapsed(sw.Elapsed)}) ***");
            Console.WriteLine(Chalk.Bold.Green[$"{_options.ModNameCanonical} ready to run."]);
            Console.WriteLine();

            return new BuildResult(true, sw.Elapsed, outputPaths, new List<string>(), timings);
        }
        catch (BuildException ex)
        {
            BuildUtilities.PlayFailureSound();
            PrintErrorHeader($"BUILD FAILED");
            Console.WriteLine(Chalk.Red[$"Error: {ex.Message}"]);
            Console.WriteLine(Chalk.Gray[$"Stack trace: {ex.StackTrace}"]);
            Console.WriteLine();
            return new BuildResult(false, sw.Elapsed, outputPaths, new List<string> { ex.Message }, timings);
        }
        catch (Exception ex)
        {
            BuildUtilities.PlayFailureSound();
            PrintErrorHeader($"BUILD FAILED - UNHANDLED EXCEPTION");
            Console.WriteLine(Chalk.Red[$"Error: {ex.Message}"]);
            Console.WriteLine(Chalk.Gray[$"Stack trace: {ex.StackTrace}"]);
            Console.WriteLine(Chalk.Gray[$"Full exception: {ex.ToString()}"]);
            Console.WriteLine();
            return new BuildResult(false, sw.Elapsed, outputPaths, new List<string> { ex.Message }, timings);
        }
    }

    private void ReportTimings(List<TimingRecord> timings, TimeSpan totalDuration)
    {
        if (Environment.GetEnvironmentVariable("X2MC_REPORT_TIMINGS") != "1")
            return;

        var accountedTime = timings.Sum(t => t.Seconds);
        timings.Add(new TimingRecord("Total Duration", totalDuration.TotalSeconds, ""));
        timings.Add(new TimingRecord("Unaccounted Time", totalDuration.TotalSeconds - accountedTime, ""));

        var report = timings
            .OrderByDescending(t => t.Seconds)
            .Select(t => new
            {
                t.Description,
                Time = $"{t.Seconds:F2}s",
                Share = $"{(t.Seconds / totalDuration.TotalSeconds):P1}"
            });

        _logger.LogInformation(TableFormatter.Format(report));
    }

    public async Task<CleanResult> InvokeCleanAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"Deleting all cached build artifacts for {_options.ModNameCanonical}...");
        var deletedPaths = new List<string>();

        var buildCachePath = _options.BuildCachePath;
        if (Directory.Exists(buildCachePath))
        {
            Console.WriteLine($"Removing BuildCache: {buildCachePath}");
            await _mirror.DeleteAsync(buildCachePath, recursive: true, ct);
            deletedPaths.Add(buildCachePath);
        }

        var lastBuildDetailsPath = Path.Combine(_options.SdkPath, "XComGame", "lastBuildDetails.json");
        if (File.Exists(lastBuildDetailsPath))
        {
            Console.WriteLine($"Removing lastBuildDetails.json: {lastBuildDetailsPath}");
            await _mirror.DeleteAsync(lastBuildDetailsPath, ct: ct);
            deletedPaths.Add(lastBuildDetailsPath);
        }

        var shaderCachePath = Path.Combine(_options.SdkPath, "XComGame", "Content", "LocalShaderCache-PC-D3D-SM4.upk");
        if (File.Exists(shaderCachePath))
        {
            Console.WriteLine($"Removing LocalShaderCache: {shaderCachePath}");
            await _mirror.DeleteAsync(shaderCachePath, ct: ct);
            deletedPaths.Add(shaderCachePath);
        }

        var manifestPath = Path.Combine(_options.ProjectRoot, "BuildCache", "CompiledModPackages.txt");
        var scriptPackages = new List<string>();
        
        if (File.Exists(manifestPath))
        {
            Console.WriteLine($"Reading compiled packages from manifest: {manifestPath}");
            var lines = File.ReadAllLines(manifestPath);
            scriptPackages.AddRange(lines.OfType<string>().Where(l => !string.IsNullOrWhiteSpace(l)));
            await _mirror.DeleteAsync(manifestPath, ct: ct);
            deletedPaths.Add(manifestPath);
        }
        else
        {
            var modSrcPath = Path.Combine(_options.ModSrcRoot, "Src");
            if (Directory.Exists(modSrcPath))
            {
                foreach (var dir in Directory.GetDirectories(modSrcPath))
                {
                    scriptPackages.Add(Path.GetFileName(dir));
                }
            }
        }

        foreach (var pkg in scriptPackages)
        {
            var sdkScriptPath = Path.Combine(_options.SdkPath, "XComGame", "Script", $"{pkg}.u");
            var sdkFinalScriptPath = Path.Combine(_options.SdkPath, "XComGame", "ScriptFinalRelease", $"{pkg}.u");
            var gameScriptPath = Path.Combine(_options.GamePath, "XComGame", "Script", $"{pkg}.u");

            foreach (var path in new[] { sdkScriptPath, sdkFinalScriptPath, gameScriptPath })
            {
                if (File.Exists(path))
                {
                    Console.WriteLine($"Removing script package: {path}");
                    await _mirror.DeleteAsync(path, ct: ct);
                    deletedPaths.Add(path);
                }
            }
        }

        var sdkSrcPath = Path.Combine(_options.SdkPath, "Development", "Src");
        if (Directory.Exists(sdkSrcPath))
        {
            foreach (var dir in Directory.GetDirectories(sdkSrcPath))
            {
                await _mirror.DeleteAsync(dir, recursive: true, ct);
                deletedPaths.Add(dir);
            }
            foreach (var file in Directory.GetFiles(sdkSrcPath))
            {
                await _mirror.DeleteAsync(file, ct: ct);
                deletedPaths.Add(file);
            }
        }

        var sdkModsPath = Path.Combine(_options.SdkPath, "XComGame", "Mods");
        if (Directory.Exists(sdkModsPath))
        {
            foreach (var dir in Directory.GetDirectories(sdkModsPath))
            {
                await _mirror.DeleteAsync(dir, recursive: true, ct);
                deletedPaths.Add(dir);
            }
        }

        var gameModPath = Path.Combine(_options.GamePath, "XComGame", "Mods", _options.ModNameCanonical);
        if (Directory.Exists(gameModPath))
        {
            await _mirror.DeleteAsync(gameModPath, recursive: true, ct);
            deletedPaths.Add(gameModPath);
        }

        return new CleanResult(true, deletedPaths, new());
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.SdkPath) || !Directory.Exists(_options.SdkPath))
            throw new BuildConfigurationException("SdkPath", "Path does not exist or is invalid.");
        if (string.IsNullOrWhiteSpace(_options.GamePath) || !Directory.Exists(_options.GamePath))
            throw new BuildConfigurationException("GamePath", "Path does not exist or is invalid.");
        if (string.IsNullOrWhiteSpace(_options.ModName))
            throw new BuildConfigurationException("ModName", "ModName cannot be empty.");
    }

    private async Task CopyDirectoryRecursiveAsync(string sourceDir, string destDir, CancellationToken ct)
    {
        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            await _mirror.CopyAsync(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true, ct: ct);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            await CopyDirectoryRecursiveAsync(dir, Path.Combine(destDir, new DirectoryInfo(dir).Name), ct);
        }
    }

    private async Task CookHighlanderPackagesAsync(string[] modScriptPackages, CancellationToken ct)
    {
        var commandletPath = Path.Combine(_options.SdkPath, "binaries", "Win64", "XComGame.com");
        var cookArgs = $"cookpackages -platform=pcconsole -quickanddirty -modcook -sha -multilanguagecook=INT+FRA+ITA+DEU+RUS+POL+KOR+ESN -singlethread -nopause";
        if (_options.FinalRelease) cookArgs += " -final_release";

        var exitCode = await _runner.RunProcessWithSleepAsync(commandletPath, cookArgs, new BufferingReceiver(), 0, 0, ct);
        if (exitCode != 0) throw new BuildFailureException("Highlander cooking", exitCode);
    }

    private bool HasNativePackages(string[] modScriptPackages) => modScriptPackages.Any(name => _nativeScriptPackages.Contains(name, StringComparer.OrdinalIgnoreCase));

    private string[] GetAllScriptPackages(string iniContent)
    {
        var pkgs = new List<string>();
        var lines = iniContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool inEngineSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.Equals(trimmed, "[UnrealEd.EditorEngine]", StringComparison.OrdinalIgnoreCase)) { inEngineSection = true; continue; }
            if (inEngineSection)
            {
                if (trimmed.StartsWith("[")) break;
                if (trimmed.Contains("ModEditPackages=", StringComparison.OrdinalIgnoreCase))
                {
                    var pkg = trimmed.Split('=')[1].Trim();
                    if (!string.IsNullOrEmpty(pkg)) pkgs.Add(pkg);
                }
            }
        }
        return pkgs.Where(pkg => !_nativeScriptPackages.Contains(pkg, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private string[] GetModScriptPackagesFromSrc()
    {
        var pkgs = new List<string>();
        var modSrcPath = Path.Combine(_options.ModSrcRoot, "Src");
        if (Directory.Exists(modSrcPath)) foreach (var dir in Directory.GetDirectories(modSrcPath)) pkgs.Add(Path.GetFileName(dir));
        foreach (var includeDir in _options.IncludePaths) if (Directory.Exists(includeDir)) foreach (var dir in Directory.GetDirectories(includeDir)) pkgs.Add(Path.GetFileName(dir));
        return pkgs.Where(pkg => !_nativeScriptPackages.Contains(pkg, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task CopyScriptPackagesAsync(string[] modScriptPackages, string stagingPath, CancellationToken ct)
    {
        var stagingScriptPath = Path.Combine(stagingPath, "Script");
        if (!Directory.Exists(stagingScriptPath)) Directory.CreateDirectory(stagingScriptPath);
        foreach (var name in modScriptPackages)
        {
            var packagePath = Path.Combine(_options.SdkPath, "XComGame", "Script", $"{name}.u");
            if (File.Exists(packagePath)) await _mirror.CopyAsync(packagePath, Path.Combine(stagingScriptPath, $"{name}.u"), ct: ct);
        }
    }

    private async Task CleanLeftoverScriptsAsync(string[] modScriptPackages, CancellationToken ct)
    {
        var pathsToClean = new[] { Path.Combine(_options.SdkPath, "XComGame", "Script"), Path.Combine(_options.SdkPath, "XComGame", "ScriptFinalRelease"), Path.Combine(_options.GamePath, "XComGame", "Script") };
        foreach (var name in modScriptPackages) foreach (var basePath in pathsToClean)
        {
            var scriptPath = Path.Combine(basePath, $"{name}.u");
            if (File.Exists(scriptPath)) await _mirror.DeleteAsync(scriptPath, ct: ct);
        }
    }

    private async Task GenerateXComModFileAsync(string stagingPath, CancellationToken ct)
    {
        var xcomModPath = Path.Combine(stagingPath, $"{_options.ModNameCanonical}.XComMod");
        var content = $"[mod]{Environment.NewLine}publishedFileId=-1{Environment.NewLine}Title={_options.ModName}{Environment.NewLine}Description={Environment.NewLine}RequiresXPACK=true";
        await File.WriteAllTextAsync(xcomModPath, content, ct);
    }

    private string FindProjectDirectory(string root)
    {
        var x2projFiles = Directory.GetFiles(root, "*.x2proj", SearchOption.AllDirectories);
        return x2projFiles.Length > 0 ? Path.GetDirectoryName(x2projFiles[0]) ?? root : root;
    }

    private async Task CopySrcFolderAsync(string includeDir, string sdkDevSrcPath, Dictionary<string, (string Path, int Line)> definedMacros, CancellationToken ct)
    {
        if (!Directory.Exists(includeDir)) return;
        
        _logger.LogInformation($"Mirroring sources from {includeDir} to {sdkDevSrcPath}");
        await CopyDirectoryRecursiveAsync(includeDir, sdkDevSrcPath, ct);

        // Replicate build_common.ps1 logic for extra_globals.uci sonora.
        var extraGlobalsFile = Path.Combine(includeDir, "extra_globals.uci");
        if (File.Exists(extraGlobalsFile))
        {
            var targetGlobalsFile = Path.Combine(sdkDevSrcPath, "Core", "Globals.uci");
            _logger.LogInformation($"Appending {extraGlobalsFile} to {targetGlobalsFile}");
            
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"// Macros included from {extraGlobalsFile}");
            sb.AppendLine(await File.ReadAllTextAsync(extraGlobalsFile, ct));
            
            await File.AppendAllTextAsync(targetGlobalsFile, sb.ToString(), ct);
            ParseMacroFile(extraGlobalsFile, definedMacros);
        }
    }

    private void ParseMacroFile(string filePath, Dictionary<string, (string Path, int Line)> definedMacros)
    {
        if (!File.Exists(filePath)) return;
        var lines = File.ReadAllLines(filePath);
        for (int i = 0; i < lines.Length; i++)
        {
            var defineMatch = Regex.Match(lines[i].Trim(), @"^`define\s+([a-zA-Z0-9_]+)");
            if (defineMatch.Success) definedMacros[defineMatch.Groups[1].Value] = (filePath, i + 1);
        }
    }

    /// <summary>
    /// Executes the two-pass compilation flow. 
    /// Phase 1 builds the base mod binary (even if it fails linkage).
    /// Phase 2 compiles the mod along with its dependent packages using the newly created binary.
    /// This mirrors the successful manual PowerShell build pattern.
    /// </summary>
    /// <param name="iniHandler">The INI handler for configuration stability.</param>
    /// <param name="targetIni">The path to the SDK's XComEngine.ini.</param>
    /// <param name="originalContent">The original content of the INI before modification.</param>
    /// <param name="dependentPackages">The list of dependent mods that require linkage.</param>
    /// <param name="receiver">The output receiver for compilation logs.</param>
    /// <param name="ct">The cancellation token.</param>
    private async Task ExecuteTwoPassCompilationAsync(IniHandler iniHandler, string targetIni, string originalContent, List<string> dependentPackages, OutputReceiver receiver, CancellationToken ct)
    {
        PrintInfoHeader("STARTING TWO-PASS COMPILATION FLOW");
        _logger.LogInformation("Starting Two-Pass compilation flow (Mirroring Manual PowerShell Execution)");
        
        // Prepare INI once for BOTH passes to ensure environment stability.
        // This prevents Unreal from deleting binaries or re-compiling unnecessarily between runs.
        string passContent = iniHandler.PrepareModCompilationIni(originalContent, _options.ModNameCanonical, dependentPackages);
        await File.WriteAllTextAsync(targetIni, passContent, ct);

        // Also update staging path for consistency
        string stagingConfigDir = Path.Combine(_options.StagingPath, "Config");
        string stagingIniPath = Path.Combine(stagingConfigDir, "XComEngine.ini");
        Directory.CreateDirectory(stagingConfigDir);
        await File.WriteAllTextAsync(stagingIniPath, passContent, ct);

        // Pass 1
        PrintInfoHeader("PHASE 1: INITIAL COMPILATION", ConsoleColor.Cyan);
        await ExecuteCompilationPassAsync(iniHandler, targetIni, originalContent, dependentPackages, passContent, receiver, ct, passNumber: 1);

        // Pass 2
        Console.WriteLine();
        PrintInfoHeader("PHASE 2: FINAL LINKAGE", ConsoleColor.Cyan);
        _logger.LogInformation("Phase 1 complete. Starting Phase 2 for final linkage...");
        await ExecuteCompilationPassAsync(iniHandler, targetIni, originalContent, dependentPackages, passContent, receiver, ct, passNumber: 2);
    }

    /// <summary>
    /// Executes a single phase of the compilation flow.
    /// Phase 1 uses a forced-failure recovery mechanism to ensure the binary is created.
    /// </summary>
    /// <param name="iniHandler">The INI handler.</param>
    /// <param name="targetIni">The path to the target INI.</param>
    /// <param name="originalContent">Original INI content.</param>
    /// <param name="dependentPackages">List of dependent packages.</param>
    /// <param name="passContent">The prepared INI content for the mod build.</param>
    /// <param name="receiver">The output receiver.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="passNumber">The phase number (1 or 2).</param>
    private async Task<bool> ExecuteCompilationPassAsync(IniHandler iniHandler, string targetIni, string originalContent, List<string> dependentPackages, string passContent, OutputReceiver receiver, CancellationToken ct, int passNumber)
    {
        // 1. Prepare environment for base compilation (Mod removed from INI)
        string baseIniContent = PrepareBaseIniContent(originalContent, _options.ModNameCanonical);
        await File.WriteAllTextAsync(targetIni, baseIniContent, ct);
        if (!await _compiler.CompileBaseAsync(_options, receiver, ct)) return false;

        // 2. Restore mod environment for mod compilation
        await File.WriteAllTextAsync(targetIni, passContent, ct);
        
        // Small delay in Phase 2 to ensure file handles are released by the commandlet from Phase 1
        if (passNumber == 2) await Task.Delay(5000, ct);

        string compileTarget = _options.ModNameCanonical;
        bool success = false;
        try { success = await _compiler.CompileModAsync(compileTarget, _options.StagingPath, _options, receiver, ct); }
        catch (Exception) when (passNumber == 1) { success = false; }

        if (!success && passNumber == 1)
        {
            // Phase 1 Forced failure recovery logic:
            // If the .u file was successfully created, we consider it a "successful" first pass
            // as linkage failure is expected during initial creation of Two-Pass mods.
            if (File.Exists(Path.Combine(_options.SdkPath, "XComGame", "Script", $"{_options.ModNameCanonical}.u")))
            {
                LogPhase1ExpectedFailure(compileTarget);
                return true;
            }
        }
        
        if (success && passNumber == 1)
        {
            Console.WriteLine(Chalk.Green["# [PHASE 1] Succeeded normally. Proceeding to Phase 2 for final linkage... sonora."]);
        }

        if (!success)
        {
            _logger.LogError($"Pass {passNumber}: Mod script compilation failed");
            return false;
        }

        _logger.LogInformation($"Pass {passNumber}: Compilation completed successfully");
        return true;
    }

    /// <summary>
    /// Executes a standard single-pass compilation.
    /// Per user request, the SDK's XComEngine.ini is NOT modified during this flow.
    /// </summary>
    private async Task ExecuteSinglePassCompilationAsync(IniHandler iniHandler, string targetIni, string originalContent, OutputReceiver receiver, CancellationToken ct)
    {
        // For single-pass compilation, we do NOT modify the SDK INI as per user request. sonora.
        var dependentPackages = iniHandler.GetDependantPackages(originalContent);
        
        PrintInfoHeader("STARTING SINGLE-PASS COMPILATION FLOW");
        _logger.LogInformation("Starting Single-Pass compilation flow (INI remains untouched)");

        // We still need to compile base if requested or needed, but typically we just compile the mod.
        if (!await _compiler.CompileBaseAsync(_options, receiver, ct)) throw new BuildFailureException("Base script compilation", 1);
        
        var compileTarget = _options.ModNameCanonical;
        if (dependentPackages.Count > 0) compileTarget = $"{_options.ModNameCanonical} {string.Join(" ", dependentPackages)}";
        
        if (!await _compiler.CompileModAsync(compileTarget, _options.StagingPath, _options, receiver, ct)) throw new BuildFailureException("Mod script compilation", 1);
    }

    private string PrepareBaseIniContent(string originalContent, string mainModName)
    {
        var lines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
        RemoveSectionStatic(lines, "[X2ModCompiler.DependantPackages]");
        RemovePackageFromEngineSectionStatic(lines, mainModName);
        return string.Join(Environment.NewLine, lines);
    }

    private static void RemoveSectionStatic(List<string> lines, string sectionHeader)
    {
        int start = -1, end = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), sectionHeader, StringComparison.OrdinalIgnoreCase)) start = i;
            else if (start != -1 && lines[i].Trim().StartsWith("[")) { end = i; break; }
        }
        if (start != -1) lines.RemoveRange(start, (end == -1 ? lines.Count : end) - start);
    }

    private static void RemovePackageFromEngineSectionStatic(List<string> lines, string packageName)
    {
        int start = -1;
        for (int i = 0; i < lines.Count; i++) if (string.Equals(lines[i].Trim(), "[UnrealEd.EditorEngine]", StringComparison.OrdinalIgnoreCase)) { start = i; break; }
        if (start == -1) return;
        for (int i = start + 1; i < lines.Count; i++)
        {
            if (lines[i].Trim().StartsWith("[")) break;
            if (lines[i].Trim().Contains($"={packageName}", StringComparison.OrdinalIgnoreCase)) { lines.RemoveAt(i); i--; }
        }
    }
}