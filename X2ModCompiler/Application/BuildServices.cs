using X2ModCompiler.Compilation;
using X2ModCompiler.Cooking;
using X2ModCompiler.Core.Validation;
using X2ModCompiler.Tracking;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application;

/// <summary>
/// Facade for build-related services to reduce BuildController constructor parameters.
/// This class groups related dependencies to simplify dependency injection and improve maintainability.
/// </summary>
public sealed class BuildServices
{
    /// <summary>Gets the build tracker for maintaining change fingerprints.</summary>
    public BuildTracker Tracker { get; }

    /// <summary>Gets the script compiler for UnrealScript compilation.</summary>
    public ScriptCompiler Compiler { get; }

    /// <summary>Gets the asset cooker for cooking mod assets.</summary>
    public AssetCooker Cooker { get; }

    /// <summary>Gets the file mirror for file synchronization operations.</summary>
    public IFileMirrorParity Mirror { get; }

    /// <summary>Gets the process runner for executing external processes.</summary>
    public IProcessRunner ProcessRunner { get; }

    /// <summary>Gets the shader precompiler for precompiling shaders.</summary>
    public ShaderPrecompiler ShaderPrecompiler { get; }

    /// <summary>Gets the missing uncooked copier for copying uncooked assets.</summary>
    public MissingUncookedCopier MissingUncookedCopier { get; }

    /// <summary>Gets the project synchronizer for syncing project files.</summary>
    public ProjectSynchronizer ProjectSynchronizer { get; }

    /// <summary>Gets the file processor for configuration file validation.</summary>
    public FileProcessor FileProcessor { get; }

    /// <summary>Gets the script cleaner for cleaning up compiled scripts.</summary>
    public ScriptCleaner ScriptCleaner { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildServices"/> class.
    /// </summary>
    /// <param name="tracker">The build tracker.</param>
    /// <param name="compiler">The script compiler.</param>
    /// <param name="cooker">The asset cooker.</param>
    /// <param name="mirror">The file mirror.</param>
    /// <param name="processRunner">The process runner.</param>
    /// <param name="shaderPrecompiler">The shader precompiler.</param>
    /// <param name="missingUncookedCopier">The missing uncooked copier.</param>
    /// <param name="projectSynchronizer">The project synchronizer.</param>
    /// <param name="fileProcessor">The file processor.</param>
    /// <param name="scriptCleaner">The script cleaner.</param>
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
