using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Base class for build steps that provides common functionality.
/// Reduces code duplication by handling logging, error handling, and step naming.
/// </summary>
public abstract class BuildStepBase : IBuildStep
{
    /// <summary>
    /// Gets the name of this build step.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the logger for this step.
    /// </summary>
    protected readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the BuildStepBase class.
    /// </summary>
    /// <param name="name">The name of the step.</param>
    /// <param name="logger">The logger.</param>
    protected BuildStepBase(string name, ILogger logger)
    {
        Name = name;
        _logger = logger;
    }

    /// <summary>
    /// Executes the build step with common error handling and logging.
    /// </summary>
    /// <param name="options">The build options.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if the step succeeded; otherwise, false.</returns>
    public virtual async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation(LogColors.StepHeader(Name));
            return await ExecuteStepAsync(options, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning(LogColors.Warning($"{Name} was cancelled"));
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(LogColors.Error($"{Name} failed: {ex.Message}"));
            _logger.LogDebug(ex.ToString());
            return false;
        }
    }

    /// <summary>
    /// Executes the step's core logic. Implemented by derived classes.
    /// </summary>
    /// <param name="options">The build options.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if the step succeeded; otherwise, false.</returns>
    protected abstract Task<bool> ExecuteStepAsync(BuildOptions options, CancellationToken ct);
}
