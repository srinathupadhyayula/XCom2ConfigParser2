using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Utilities;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Synchronizes the XCOM 2 project file (.x2proj) with the disk content to ensure all assets are tracked.
/// </summary>
public class ProjectSyncStep : BuildStepBase
{
    private readonly ProjectSynchronizer _synchronizer;

    public ProjectSyncStep(ProjectSynchronizer synchronizer, ILogger<ProjectSyncStep> logger)
        : base("Project Synchronization", logger)
    {
        _synchronizer = synchronizer;
    }

    protected override Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info("Synchronizing project files..."));
        // This mirrors BuildController.Step_RegenerateItemGroup logic
        _synchronizer.Synchronize(options.ProjectRoot);
        return Task.FromResult(true);
    }
}
