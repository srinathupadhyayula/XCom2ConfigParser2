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
        _logger.LogInformation($"{progressWord} {description}...");
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await step();
            stopwatch.Stop();
            
            _logger.LogInformation($"{completedWord} {description} in {stopwatch.Elapsed.TotalSeconds:F6}s");
            
            timings.Add(new TimingRecord(
                $"{progressWord} {description}",
                stopwatch.Elapsed.TotalSeconds,
                ""));
            
            return result;
        }
        catch
        {
            stopwatch.Stop();
            _logger.LogError($"{progressWord} {description} FAILED after {stopwatch.Elapsed.TotalSeconds:F6}s");
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
            // Echo paths at start (mimics PowerShell _ConfirmPaths)
            Console.WriteLine($"SDK Path: {_options.SdkPath}");
            Console.WriteLine($"Game Path: {_options.GamePath}");

            Console.WriteLine($"Starting build for {_options.ModName}");
            ValidateConfiguration();

            // 0. Synchronize Project (ItemGroup regeneration)
            var x2projPath = Path.Combine(_options.ModSrcRoot, $"{_options.ModName}.x2proj");
            PerformStep(() => _projectSynchronizer.Synchronize(x2projPath), "Regenerating", "Regenerated", "ItemGroup in .x2proj file", timings);

            // Clean additional mods if specified (must be before copying mod to staging)
            if (_options.CleanMods.Count > 0)
            {
                await PerformStepAsync(
                    async () =>
                    {
                        foreach (var modName in _options.CleanMods)
                        {
                            var cleanDir = Path.Combine(_options.SdkPath, "XComGame", "Mods", modName);
                            if (Directory.Exists(cleanDir))
                            {
                                Console.WriteLine($"Cleaning {modName}...");
                                await _mirror.DeleteAsync(cleanDir, recursive: true, ct: ct);
                            }
                        }
                    },
                    "Cleaning", "Cleaned", "additional mods", timings);
            }

            var projectDir = FindProjectDirectory(_options.ModSrcRoot);
            var modSrcPath = Path.Combine(projectDir, "Src");

            // 1. Preparation: Copy mod to staging area
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
            await PerformStepAsync(() => _shaderPrecompiler.PrecompileAsync(_options, ct), "Precompiling", "Precompiled", "shaders", timings);

            // Execute pre-make hooks
            if (_options.PreMakeHooks.Count > 0)
            {
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


            // 2. Compile (Single Pass Parity with build_common.ps1)
            var receiver = new MakeOutputReceiver(new[] { _options.ModSrcRoot }.Concat(_options.IncludePaths).ToArray());
            await PerformStepAsync(
                async () =>
                {
                    var settingsLoader = new XCom2ModCompiler.Configuration.SettingsLoader(_options.ProjectRoot);
                    var settings = settingsLoader.Load();
                    // Target the SDK's XComEngine.ini (PS1 parity: UCC make always reads from SDK config)
                    string targetIni = Path.Combine(_options.SdkPath, "XComGame", "Config", "XComEngine.ini");
                    var iniHandler = new IniHandler(_options.ProjectRoot, settings.IniRoots);

                    if (!string.IsNullOrEmpty(targetIni) && File.Exists(targetIni))
                    {
                        string originalContent = await File.ReadAllTextAsync(targetIni, ct);
                        
                        // Pre-stage the configuration to the staging path
                        string stagingConfigDir = Path.Combine(_options.StagingPath, "Config");
                        string stagingIniPath = Path.Combine(stagingConfigDir, "XComEngine.ini");
                        Directory.CreateDirectory(stagingConfigDir);

                        // Prepare the INI with all packages (including the mod and any dependencies)
                        // PS1 parity: [UnrealEd.EditorEngine] +ModEditPackages=...
                        var dependentMods = iniHandler.GetDependantPackages(originalContent);
                        var stagedContent = iniHandler.PrepareStagedIni(originalContent, _options.ModNameCanonical, dependentMods);
                        
                        // We must write to the actual target INI because UCC make reads from the SDK/Game config, not the staging dir
                        // We restore it in the finally block below
                        await File.WriteAllTextAsync(targetIni, stagedContent, ct);
                        await File.WriteAllTextAsync(stagingIniPath, stagedContent, ct); 

                        try
                        {
                            // Clean output .u files (PS1 parity lines 517-526)
                            _logger.LogInformation("Cleaning compiled scripts to ensure fresh build...");
                            var uFile = Path.Combine(_options.SdkPath, "XComGame", "Script", $"{_options.ModNameCanonical}.u");
                            if (File.Exists(uFile))
                            {
                                File.Delete(uFile);
                            }
                            foreach (var depMod in dependentMods)
                            {
                                var depUFile = Path.Combine(_options.SdkPath, "XComGame", "Script", $"{depMod}.u");
                                if (File.Exists(depUFile))
                                {
                                    File.Delete(depUFile);
                                }
                            }

                            bool success = await _compiler.CompileBaseAsync(_options, receiver, ct);
                            if (!success)
                            {
                                throw new BuildFailureException("Script compilation", 1);
                            }

                            // Record output paths for successful build reporting
                            var allPackages = GetAllScriptPackages(stagedContent);
                            foreach (var pkg in allPackages)
                            {
                                outputPaths.Add(Path.Combine(_options.SdkPath, "XComGame", "Script", $"{pkg}.u"));
                            }
                        }
                        finally
                        {
                            _logger.LogInformation("Restoring XComEngine.ini...");
                            await File.WriteAllTextAsync(targetIni, originalContent, ct);
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

            // Record Core.u timestamp (needed for incremental build detection)
            await PerformStepAsync(
                () => _tracker.RecordCoreTimestampAsync(_options.SdkPath, ct),
                "Recording", "Recorded", "Core.u timestamp", timings);

            // Copy script packages to staging
            string currentIniPath = Path.Combine(_options.SdkPath, "XComGame", "Config", "XComEngine.ini");
            string currentIniContent = File.Exists(currentIniPath) ? await File.ReadAllTextAsync(currentIniPath, ct) : "";
            var allScriptPackages = GetAllScriptPackages(currentIniContent);
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
                    await PerformStepAsync(
                        () => CookHighlanderPackagesAsync(allScriptPackages, ct),
                        "Cooking", "Cooked", "Highlander packages", timings);
                }
                else if (isHighlander && _options.Debug)
                {
                    Console.WriteLine("Skipping Highlander cooking as debug build");
                }
            }

            // Shader precompilation (needs to happen before asset cooking)
            await PerformStepAsync(() => _shaderPrecompiler.PrecompileAsync(_options, ct), "Precompiling", "Precompiled", "shaders", timings);

            // 4. Cooking (if content options present)
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

            // 4.5 Copy missing uncooked
            await PerformStepAsync(
                () => _missingUncookedCopier.CopyMissingAsync(_options, contentOptions, ct),
                "Copying", "Copied", "missing uncooked packages", timings);

            // 5. Final Copy to Game Dir (mimics PowerShell _FinalCopy)
            Console.WriteLine("Copying built mod to game directory...");
            await PerformStepAsync(
                async () =>
                {
                    await _mirror.MirrorAsync(_options.StagingPath, _options.FinalModPath, ct: ct);

                    // Remove staging directory after successful copy (mimics PowerShell)
                    Console.WriteLine($"Removing source staging directory {_options.StagingPath} after successful move to {_options.FinalModPath}...");
                    if (Directory.Exists(_options.StagingPath))
                    {
                        Directory.Delete(_options.StagingPath, recursive: true);
                    }
                    Console.WriteLine("Source directory removed. Move operation completed successfully.");
                },
                "Copying", "Copied", "output to final destination", timings);

            // Clean leftover scripts from SDK/Game (MUST be at the end after final copy)
            if (allScriptPackages.Length > 0)
            {
                await PerformStepAsync(
                    () => CleanLeftoverScriptsAsync(allScriptPackages, ct),
                    "Cleaning", "Cleaned", "leftover script packages from SDK/Game directory", timings);
            }

            // Record fingerprint
            var coreTime = File.GetLastWriteTime(Path.Combine(_options.SdkPath, "XComGame", "Script", "Core.u"));
            var globalsHash = BuildTracker.ComputeFileHash(Path.Combine(_options.SdkPath, "Development", "Src", "Core", "Globals.uci"));

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
                globalsHash,
                coreTime,
                DateTime.UtcNow,
                timestamps), ct);

            Console.WriteLine();
            Console.WriteLine($"Build completed successfully in {sw.Elapsed.TotalSeconds:F2}s");

            // Report timings if requested
            ReportTimings(timings, sw.Elapsed);

            // Success message with sound (mimics PowerShell SuccessMessage)
            BuildUtilities.PlaySuccessSound();
            Console.WriteLine();
            Console.WriteLine(Chalk.Bold.Green[$"*** SUCCESS! ({BuildUtilities.FormatElapsed(sw.Elapsed)}) ***"]);
            Console.WriteLine(Chalk.Green[$"{_options.ModNameCanonical} ready to run."]);

            return new BuildResult(true, sw.Elapsed, outputPaths, new List<string>(), timings);
        }
        catch (BuildException ex)
        {
            Console.WriteLine();
            Console.WriteLine(Chalk.Red[$"Build failed: {ex.Message}"]);
            Console.WriteLine(Chalk.Gray[$"Stack trace: {ex.StackTrace}"]);
            BuildUtilities.PlayFailureSound();
            return new BuildResult(false, sw.Elapsed, outputPaths, new List<string> { ex.Message }, timings);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine(Chalk.Red[$"Unhandled exception: {ex.Message}"]);
            Console.WriteLine(Chalk.Gray[$"Stack trace: {ex.StackTrace}"]);
            Console.WriteLine(Chalk.Gray[$"Full exception: {ex.ToString()}"]);
            BuildUtilities.PlayFailureSound();
            return new BuildResult(false, sw.Elapsed, outputPaths, new List<string> { ex.Message }, timings);
        }
    }

    /// <summary>
    /// Reports build timings if X2MC_REPORT_TIMINGS environment variable is set.
    /// </summary>
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

        // Clean BuildCache directory
        var buildCachePath = _options.BuildCachePath;
        if (Directory.Exists(buildCachePath))
        {
            Console.WriteLine($"Removing BuildCache: {buildCachePath}");
            await _mirror.DeleteAsync(buildCachePath, recursive: true, ct);
            deletedPaths.Add(buildCachePath);
        }

        // Clean lastBuildDetails.json
        var lastBuildDetailsPath = Path.Combine(_options.SdkPath, "XComGame", "lastBuildDetails.json");
        if (File.Exists(lastBuildDetailsPath))
        {
            Console.WriteLine($"Removing lastBuildDetails.json: {lastBuildDetailsPath}");
            await _mirror.DeleteAsync(lastBuildDetailsPath, ct: ct);
            deletedPaths.Add(lastBuildDetailsPath);
        }

        // Clean LocalShaderCache
        var shaderCachePath = Path.Combine(_options.SdkPath, "XComGame", "Content", "LocalShaderCache-PC-D3D-SM4.upk");
        if (File.Exists(shaderCachePath))
        {
            Console.WriteLine($"Removing LocalShaderCache: {shaderCachePath}");
            await _mirror.DeleteAsync(shaderCachePath, ct: ct);
            deletedPaths.Add(shaderCachePath);
        }

        // Clean script packages (from manifest or by scanning)
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
                Console.WriteLine($"No manifest found, scanning source directory for packages...");
                foreach (var dir in Directory.GetDirectories(modSrcPath))
                {
                    scriptPackages.Add(Path.GetFileName(dir));
                }
            }
        }

        // Clean script package files from all locations
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

        // Clean SDK\Development\Src\*
        var sdkSrcPath = Path.Combine(_options.SdkPath, "Development", "Src");
        if (Directory.Exists(sdkSrcPath))
        {
            Console.WriteLine($"Removing SDK Development\\Src contents: {sdkSrcPath}");
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

        // Clean SDK\XComGame\Mods\*
        var sdkModsPath = Path.Combine(_options.SdkPath, "XComGame", "Mods");
        if (Directory.Exists(sdkModsPath))
        {
            Console.WriteLine($"Removing SDK Mods contents: {sdkModsPath}");
            foreach (var dir in Directory.GetDirectories(sdkModsPath))
            {
                await _mirror.DeleteAsync(dir, recursive: true, ct);
                deletedPaths.Add(dir);
            }
        }

        // Clean Game\XComGame\Mods\$modName
        var gameModPath = Path.Combine(_options.GamePath, "XComGame", "Mods", _options.ModNameCanonical);
        if (Directory.Exists(gameModPath))
        {
            Console.WriteLine($"Removing game mod directory: {gameModPath}");
            await _mirror.DeleteAsync(gameModPath, recursive: true, ct);
            deletedPaths.Add(gameModPath);
        }

        // Clean custom mod destination path if provided
        if (!string.IsNullOrEmpty(_options.ModDestinationPath) && _options.ModDestinationPath != _options.GamePath)
        {
            var customModPath = Path.Combine(_options.ModDestinationPath, _options.ModNameCanonical);
            if (Directory.Exists(customModPath))
            {
                Console.WriteLine($"Removing custom mod destination: {customModPath}");
                await _mirror.DeleteAsync(customModPath, recursive: true, ct);
                deletedPaths.Add(customModPath);
            }
        }

        Console.WriteLine("Cleaned.");
        _logger.LogInformation("Clean completed successfully.");
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

        if (string.IsNullOrWhiteSpace(_options.ProjectRoot))
            throw new BuildConfigurationException("ProjectRoot", "ProjectRoot cannot be empty.");

        // Validate no script packages but features that require them are enabled
        var modSrcPath = Path.Combine(_options.ModSrcRoot, "Src");
        bool hasScriptPackages = Directory.Exists(modSrcPath) && Directory.GetDirectories(modSrcPath).Length > 0;

        if (!hasScriptPackages)
        {
            if (_options.CleanMods.Count > 0)
                throw new BuildConfigurationException("CleanMods", "AddToClean is not supported when no script packages to compile");

            if (_options.IncludePaths.Count > 0)
                throw new BuildConfigurationException("IncludePaths", "IncludeSrc is not supported when no script packages to compile");

            if (_options.PreMakeHooks.Count > 0)
                throw new BuildConfigurationException("PreMakeHooks", "AddPreMakeHook is not supported when no script packages to compile");

            if (_options.Debug)
                throw new BuildConfigurationException("Debug", "Debug build enabled but no script packages to compile");
        }
    }

    /// <summary>
    /// Copies a directory recursively without deleting existing files.
    /// Mimics PowerShell Copy-Item -Recurse behavior.
    /// </summary>
    private async Task CopyDirectoryRecursiveAsync(string sourceDir, string destDir, CancellationToken ct)
    {
        // Create destination directory if it doesn't exist
        if (!Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        // Copy all files
        var files = Directory.GetFiles(sourceDir);
        foreach (var file in files)
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            await _mirror.CopyAsync(file, destFile, overwrite: true, ct: ct);
        }

        // Copy all subdirectories recursively
        var dirs = Directory.GetDirectories(sourceDir);
        foreach (var dir in dirs)
        {
            var destSubDir = Path.Combine(destDir, new DirectoryInfo(dir).Name);
            await CopyDirectoryRecursiveAsync(dir, destSubDir, ct);
        }
    }

    /// <summary>
    /// Cooks Highlander (native) packages.
    /// Mimics PowerShell _RunCookHL() method.
    /// </summary>
    private async Task CookHighlanderPackagesAsync(string[] modScriptPackages, CancellationToken ct)
    {
        // Ensure cooker output directory exists
        if (!Directory.Exists(_options.CookerOutputPath))
        {
            Directory.CreateDirectory(_options.CookerOutputPath);
            _logger.LogInformation("Created {Path} directory", _options.CookerOutputPath);
        }

        // Copy essential cooker files from game CookedPCConsole
        var gameCookedPath = Path.Combine(_options.GamePath, "XComGame", "CookedPCConsole");
        var filesToCopy = new[] { "GuidCache.upk", "GlobalPersistentCookerData.upk", "PersistentCookerShaderData.bin" };
        
        foreach (var fileName in filesToCopy)
        {
            var srcPath = Path.Combine(gameCookedPath, fileName);
            var destPath = Path.Combine(_options.CookerOutputPath, fileName);
            
            if (!File.Exists(destPath) && File.Exists(srcPath))
            {
                _logger.LogInformation("Copying {FileName}...", fileName);
                await _mirror.CopyAsync(srcPath, destPath, ct: ct);
            }
        }

        // Copy TFC files (don't overwrite existing - /XC /XN /XO equivalent)
        _logger.LogInformation("Copying Texture File Caches...");
        if (Directory.Exists(gameCookedPath))
        {
            var tfcFiles = Directory.GetFiles(gameCookedPath, "*.tfc");
            foreach (var tfcFile in tfcFiles)
            {
                var destTfc = Path.Combine(_options.CookerOutputPath, Path.GetFileName(tfcFile));
                if (!File.Exists(destTfc))
                {
                    await _mirror.CopyAsync(tfcFile, destTfc, ct: ct);
                }
            }
        }
        _logger.LogInformation("Copied Texture File Caches.");

        // Prepare CookPackages arguments
        var cookArgs = $"cookpackages -platform=pcconsole -quickanddirty -modcook -sha -multilanguagecook=INT+FRA+ITA+DEU+RUS+POL+KOR+ESN -singlethread -nopause";
        if (_options.FinalRelease)
        {
            cookArgs += " -final_release";
        }

        // Use BufferingReceiver - only shows output on failure
        var commandletPath = Path.Combine(_options.SdkPath, "binaries", "Win64", "XComGame.com");
        var receiver = new BufferingReceiver();
        receiver.ProcessDescription = "cooking native packages";

        _logger.LogInformation("Invoking CookPackages (this may take a while)");
        var exitCode = await _runner.RunProcessWithSleepAsync(commandletPath, cookArgs, receiver, 0, 0, ct);
        
        if (exitCode != 0)
        {
            throw new BuildFailureException("Highlander cooking", exitCode);
        }
    }

    /// <summary>
    /// Checks if the mod contains any native (Highlander) packages.
    /// </summary>
    private bool HasNativePackages(string[] modScriptPackages)
    {
        foreach (var name in modScriptPackages)
        {
            if (_nativeScriptPackages.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Gets all script packages from the target XComEngine.ini ModEditPackages.
    /// </summary>
    private string[] GetAllScriptPackages(string iniContent)
    {
        var pkgs = new List<string>();
        var lines = iniContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        bool inEngineSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.Equals(trimmed, "[UnrealEd.EditorEngine]", StringComparison.OrdinalIgnoreCase))
            {
                inEngineSection = true;
                continue;
            }
            if (inEngineSection)
            {
                if (trimmed.StartsWith("[")) break;
                if (trimmed.Contains("ModEditPackages=", StringComparison.OrdinalIgnoreCase))
                {
                    var pkg = trimmed.Split('=')[1].Trim();
                    if (!string.IsNullOrEmpty(pkg))
                    {
                        pkgs.Add(pkg);
                    }
                }
            }
        }

        return pkgs
            .Where(pkg => !_nativeScriptPackages.Contains(pkg, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Copies compiled script packages to staging directory.
    /// Mimics PowerShell _CopyScriptPackages() method.
    /// </summary>
    private async Task CopyScriptPackagesAsync(string[] modScriptPackages, string stagingPath, CancellationToken ct)
    {
        _logger.LogInformation("Copying {Count} script packages to staging...", modScriptPackages.Length);

        var stagingScriptPath = Path.Combine(stagingPath, "Script");
        var stagingCookedPath = Path.Combine(stagingPath, "CookedPCConsole");

        if (!Directory.Exists(stagingScriptPath))
        {
            Directory.CreateDirectory(stagingScriptPath);
        }

        foreach (var name in modScriptPackages)
        {
            // Check if this is a native (cooked) package - Highlander
            if (_nativeScriptPackages.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                // This is a native script package - copy cooked upks
                var cookedUpkPath = Path.Combine(_options.CookerOutputPath, $"{name}.upk");
                var cookedSizePath = Path.Combine(_options.CookerOutputPath, $"{name}.upk.uncompressed_size");

                if (File.Exists(cookedUpkPath))
                {
                    if (!Directory.Exists(stagingCookedPath))
                    {
                        Directory.CreateDirectory(stagingCookedPath);
                    }
                    await _mirror.CopyAsync(cookedUpkPath, Path.Combine(stagingCookedPath, $"{name}.upk"), ct: ct);
                    Console.WriteLine(cookedUpkPath);

                    if (File.Exists(cookedSizePath))
                    {
                        await _mirror.CopyAsync(cookedSizePath, Path.Combine(stagingCookedPath, $"{name}.upk.uncompressed_size"), ct: ct);
                    }
                }
                else
                {
                    _logger.LogWarning("Native package {Name}.upk not found in cooker output", name);
                }
            }
            else
            {
                // Non-native package - copy .u script file
                var packagePath = Path.Combine(_options.SdkPath, "XComGame", "Script", $"{name}.u");
                if (File.Exists(packagePath))
                {
                    await _mirror.CopyAsync(packagePath, Path.Combine(stagingScriptPath, $"{name}.u"), ct: ct);
                    Console.WriteLine(packagePath);
                }
                else
                {
                    _logger.LogInformation("Package {Name}.u not found - likely merged into another package due to dependencies", name);
                }
            }
        }
    }

    /// <summary>
    /// Cleans leftover script packages from SDK and game directories.
    /// Mimics PowerShell _CleanLeftoverScripts() method.
    /// </summary>
    private async Task CleanLeftoverScriptsAsync(string[] modScriptPackages, CancellationToken ct)
    {
        _logger.LogInformation("Cleaning leftover scripts from SDK/Game directories...");

        var pathsToClean = new[]
        {
            Path.Combine(_options.SdkPath, "XComGame", "Script"),
            Path.Combine(_options.SdkPath, "XComGame", "ScriptFinalRelease"),
            Path.Combine(_options.GamePath, "XComGame", "Script")
        };

        foreach (var name in modScriptPackages)
        {
            foreach (var basePath in pathsToClean)
            {
                var scriptPath = Path.Combine(basePath, $"{name}.u");
                if (File.Exists(scriptPath))
                {
                    _logger.LogInformation("Cleaning leftover script: {Path}", scriptPath);
                    await _mirror.DeleteAsync(scriptPath, ct: ct);
                }
            }
        }
    }

    /// <summary>
    /// Generates the .XComMod metadata file.
    /// Mimics PowerShell _CopyModToSdk() mod metadata generation.
    /// </summary>
    private async Task GenerateXComModFileAsync(string stagingPath, CancellationToken ct)
    {
        var x2projPath = Path.Combine(_options.ModSrcRoot, $"{_options.ModName}.x2proj");
        var xcomModPath = Path.Combine(stagingPath, $"{_options.ModNameCanonical}.XComMod");

        // Ensure staging directory exists
        if (!Directory.Exists(stagingPath))
        {
            Directory.CreateDirectory(stagingPath);
        }

        // Try to read metadata from .x2proj file
        var publishedId = _options.WorkshopId;
        var title = _options.ModName;
        var description = "";

        if (File.Exists(x2projPath))
        {
            try
            {
                var x2projXml = System.Xml.Linq.XDocument.Load(x2projPath);
                var propertyGroup = x2projXml.Descendants("PropertyGroup").FirstOrDefault();
                if (propertyGroup != null)
                {
                    var steamIdElem = propertyGroup.Element("SteamPublishID");
                    if (steamIdElem != null && long.TryParse(steamIdElem.Value, out var steamId))
                    {
                        if (publishedId == -1) publishedId = steamId;
                    }

                    var nameElem = propertyGroup.Element("Name");
                    if (nameElem != null) title = nameElem.Value;

                    var descElem = propertyGroup.Element("Description");
                    if (descElem != null) description = descElem.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read mod metadata from {X2ProjPath}", x2projPath);
            }
        }

        // Override workshop ID if specified
        if (_options.WorkshopId != -1)
        {
            publishedId = _options.WorkshopId;
            _logger.LogInformation("Using override workshop ID: {WorkshopId}", publishedId);
        }

        // Write .XComMod file
        var content = $"[mod]{Environment.NewLine}publishedFileId={publishedId}{Environment.NewLine}Title={title}{Environment.NewLine}Description={description}{Environment.NewLine}RequiresXPACK=true";
        await File.WriteAllTextAsync(xcomModPath, content, ct);
        _logger.LogInformation("Generated mod metadata: {Path}", xcomModPath);
    }

    private string FindProjectDirectory(string root)
    {
        // Recursively find the folder containing a .x2proj file
        var x2projFiles = Directory.GetFiles(root, "*.x2proj", SearchOption.AllDirectories);
        if (x2projFiles.Length > 0)
        {
            return Path.GetDirectoryName(x2projFiles[0]) ?? root;
        }
        return root;
    }
    private async Task CopySrcFolderAsync(string includeDir, string sdkDevSrcPath, Dictionary<string, (string Path, int Line)> definedMacros, CancellationToken ct)
    {
        if (!Directory.Exists(includeDir)) return;

        _logger.LogInformation($"Processing source folder {includeDir}...");

        // PS1 parity: Copy-Item "$includeDir\*" "$devSrcRoot\" -Force -Recurse
        // This is INDISCRIMINATE.
        await CopyDirectoryRecursiveAsync(includeDir, sdkDevSrcPath, ct);

        // Handle extra_globals.uci if present in the include root
        var extraGlobalsFile = Path.Combine(includeDir, "extra_globals.uci");
        if (File.Exists(extraGlobalsFile))
        {
            var targetGlobalsFile = Path.Combine(sdkDevSrcPath, "Core", "Globals.uci");
            _logger.LogInformation($"Validating and appending {extraGlobalsFile} to {targetGlobalsFile}");
            
            // Validate first
            ParseMacroFile(extraGlobalsFile, definedMacros);

            // Append
            if (File.Exists(targetGlobalsFile))
            {
                string extraContent = await File.ReadAllTextAsync(extraGlobalsFile, ct);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("");
                sb.AppendLine($"// Macros included from {extraGlobalsFile}");
                sb.AppendLine(extraContent);
                await File.AppendAllTextAsync(targetGlobalsFile, sb.ToString(), ct);
            }
        }
    }

    private void ParseMacroFile(string filePath, Dictionary<string, (string Path, int Line)> definedMacros)
    {
        if (!File.Exists(filePath)) return;

        var lines = File.ReadAllLines(filePath);
        bool nextIsRedefine = false;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmedLine = line.Trim();

            // Regex match for `define NAME
            var defineMatch = Regex.Match(trimmedLine, @"^`define\s+([a-zA-Z0-9_]+)");
            if (defineMatch.Success)
            {
                var name = defineMatch.Groups[1].Value;
                if (definedMacros.TryGetValue(name, out var original))
                {
                    // build_common.ps1 parity: Only error if redefined in a DIFFERENT file AND not explicitly marked
                    if (original.Path != filePath && !nextIsRedefine)
                    {
                        throw new BuildFailureException($"Macro {name} redefined in {filePath}:{i + 1} (originally defined in {original.Path}:{original.Line})", 1);
                    }
                }
                definedMacros[name] = (filePath, i + 1);
            }

            // Regex match for `undef NAME
            var undefMatch = Regex.Match(trimmedLine, @"^`undef\s+([a-zA-Z0-9_]+)");
            if (undefMatch.Success)
            {
                var name = undefMatch.Groups[1].Value;
                definedMacros.Remove(name);
            }

            // check for explicit redefine marker (X2MBC-Redefine) in the line (comment)
            nextIsRedefine = line.Contains("X2MBC-Redefine");
        }
    }
}
