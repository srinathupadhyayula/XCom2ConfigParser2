using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Runs pre-make hooks before compilation. Mimics _RunPreMakeHooks() from build_common.ps1.
/// </summary>
public class PreMakeHooksStep : BuildStepBase
{
    private readonly BuildOptions _options;

    public PreMakeHooksStep(BuildOptions options, ILogger<PreMakeHooksStep> logger)
        : base("Run Pre-Make Hooks", logger)
    {
        _options = options;
    }

    protected override Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
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
