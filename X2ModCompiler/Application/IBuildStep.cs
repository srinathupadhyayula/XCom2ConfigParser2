using X2ModCompiler.Configuration;

namespace X2ModCompiler.Application;

/// <summary>
/// Defines a single, atomic operation within the mod build pipeline.
/// Implementing classes represent specific tasks such as compilation, cooking, or mirroring.
/// </summary>
public interface IBuildStep
{
    /// <summary>
    /// Gets the human-readable name of the build step for logging and UI display.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the build step asynchronously.
    /// </summary>
    /// <param name="options">The global build configuration options.</param>
    /// <param name="ct">A cancellation token to abort the operation.</param>
    /// <returns>A task representing the operation, returning true if the step completed successfully.</returns>
    Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct);
}
