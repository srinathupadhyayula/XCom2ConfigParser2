using Microsoft.Extensions.Logging;
using X2ModCompiler.Tracking;
using X2ModCompiler.Compilation;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Precompiles DirectX shaders and manages localized TFC/Shader assets for the mod.
/// </summary>
public class ShaderStep : IBuildStep
{
    private readonly ShaderPrecompiler _shaderPrecompiler;
    private readonly ILogger<ShaderStep> _logger;

    public ShaderStep(ShaderPrecompiler shaderPrecompiler, ILogger<ShaderStep> logger)
    {
        _shaderPrecompiler = shaderPrecompiler;
        _logger = logger;
    }

    public string Name => "Shader Precompilation";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        if (options.CompileOnly)
        {
            _logger.LogInformation("Compile-Only mode: Skipping shader precompilation.");
            return true;
        }

        _logger.LogInformation("Starting shader precompilation...");
        await _shaderPrecompiler.PrecompileAsync(options, ct);
        return true;
    }
}
