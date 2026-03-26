using Microsoft.Extensions.Logging;
using X2ModCompiler.Tracking;
using X2ModCompiler.Compilation;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Precompiles DirectX shaders and manages localized TFC/Shader assets for the mod.
/// </summary>
public class ShaderStep : BuildStepBase
{
    private readonly ShaderPrecompiler _shaderPrecompiler;

    public ShaderStep(ShaderPrecompiler shaderPrecompiler, ILogger<ShaderStep> logger)
        : base("Shader Precompilation", logger)
    {
        _shaderPrecompiler = shaderPrecompiler;
    }

    protected override async Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
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
