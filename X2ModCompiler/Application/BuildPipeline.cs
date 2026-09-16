using Kokuban;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using X2ModCompiler.Compilation;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application;

/// <summary>
/// Orchestrates the execution of multiple <see cref="IBuildStep"/>s, providing timing and logging.
/// </summary>
public class BuildPipeline
{
    private readonly List<IBuildStep> _steps = new();
    private readonly ILogger<BuildPipeline> _logger;

    public BuildPipeline(ILogger<BuildPipeline> logger)
    {
        _logger = logger;
    }

    public void AddStep(IBuildStep step)
    {
        _steps.Add(step);
    }

    public int GetStepCount() => _steps.Count;

    public async Task<List<BuildTimingRecord>> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        var timings = new List<BuildTimingRecord>();

        System.Console.Error.WriteLine($"[PIPELINE DEBUG] Pipeline has {_steps.Count} steps: {string.Join(", ", _steps.Select(s => s.Name))}");

        foreach (var step in _steps)
        {
            System.Console.Error.WriteLine($"[PIPELINE] Executing step: {step.Name}");
            _logger.LogInformation($">>> STARTING STEP: {step.Name}");
            var sw = Stopwatch.StartNew();

            bool success = false;
            string? errorMessage = null;

            try
            {
                success = await step.ExecuteAsync(options, ct);
                if (!success) errorMessage = "Step returned failure";
            }
            catch (Exception ex)
            {
                _logger.LogError(LogColors.Error($"Step {step.Name} failed with exception: {ex.Message}"));
                errorMessage = ex.Message;
                success = false;
            }

            sw.Stop();
            timings.Add(new BuildTimingRecord(step.Name, sw.Elapsed.TotalSeconds, success ? "SUCCESS" : "FAILED", errorMessage));

            if (!success)
            {
                _logger.LogError(LogColors.Error($"Exiting pipeline: {step.Name} failed. {errorMessage}"));
                // Don't break on validation step failure - config validation is non-fatal
                if (step.Name != "Config Validation")
                {
                    break;
                }
            }
        }

        return timings;
    }
}

