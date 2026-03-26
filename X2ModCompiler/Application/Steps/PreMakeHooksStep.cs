using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Runs pre-make hooks before compilation. Mimics _RunPreMakeHooks() from build_common.ps1.
/// </summary>
public class PreMakeHooksStep : IBuildStep
{
    private readonly BuildOptions _options;
    private readonly ILogger<PreMakeHooksStep> _logger;

    public PreMakeHooksStep(BuildOptions options, ILogger<PreMakeHooksStep> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string Name => "Run Pre-Make Hooks";

    public Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info("Running pre-make hooks..."));

        foreach (var hook in _options.PreMakeHooks)
        {
            try
            {
                hook.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Pre-make hook failed: {ex.Message}");
            }
        }

        _logger.LogInformation(LogColors.Success("Ran pre-make hooks."));
        return Task.FromResult(true);
    }
}
