using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Utilities;
using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Synchronizes the XCOM 2 project file (.x2proj) with the disk content to ensure all assets are tracked.
/// </summary>
public class ProjectSyncStep : IBuildStep
{
    private readonly ProjectSynchronizer _synchronizer;
    private readonly ILogger<ProjectSyncStep> _logger;

    public ProjectSyncStep(ProjectSynchronizer synchronizer, ILogger<ProjectSyncStep> logger)
    {
        _synchronizer = synchronizer;
        _logger = logger;
    }

    public string Name => "Project Synchronization";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation(LogColors.Info("Synchronizing project files..."));
        // This mirrors BuildController.Step_RegenerateItemGroup logic
        _synchronizer.Synchronize(options.ProjectRoot);
        return await Task.FromResult(true);
    }
}
