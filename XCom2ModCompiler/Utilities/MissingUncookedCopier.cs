using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XCom2ModCompiler.Configuration;

namespace XCom2ModCompiler.Utilities;

/// <summary>
/// Handles the copying of required base game asset packages into the mod's staging environment.
/// This is typically used for 'uncooked' or shared packages that the mod depends on but are not
/// included in the mod's own source content.
/// </summary>
public class MissingUncookedCopier
{
    private readonly IFileMirrorParity _mirror;
    private readonly ILogger<MissingUncookedCopier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingUncookedCopier"/> class.
    /// </summary>
    /// <param name="mirror">The file mirroring service for high-performance file copying.</param>
    /// <param name="logger">The logger for synchronization diagnostics.</param>
    public MissingUncookedCopier(IFileMirrorParity mirror, ILogger<MissingUncookedCopier> logger)
    {
        _mirror = mirror;
        _logger = logger;
    }

    /// <summary>
    /// Asynchronously copies specified missing uncooked packages from the game's installation to the mod's staging directory.
    /// </summary>
    /// <param name="options">The global build options containing path information.</param>
    /// <param name="contentOptions">The content-specific options containing the list of required packages.</param>
    /// <param name="ct">A cancellation token to abort the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual async Task CopyMissingAsync(BuildOptions options, ContentOptions contentOptions, CancellationToken ct)
    {
        if (contentOptions.MissingUncooked == null || !contentOptions.MissingUncooked.Any())
        {
            _logger.LogInformation("No missing uncooked packages specified.");
            return;
        }

        _logger.LogInformation($"Copying {contentOptions.MissingUncooked.Count} missing uncooked packages from game...");

        var gameCookedDir = Path.Combine(options.GamePath, "XComGame", "CookedPCConsole");
        var stagingCookedDir = Path.Combine(options.StagingPath, "CookedPCConsole");

        if (!Directory.Exists(stagingCookedDir))
        {
            Directory.CreateDirectory(stagingCookedDir);
        }

        foreach (var package in contentOptions.MissingUncooked)
        {
            var src = Path.Combine(gameCookedDir, package);
            var dst = Path.Combine(stagingCookedDir, package);
            
            _logger.LogInformation($"Copying {package}...");
            await _mirror.CopyAsync(src, dst, true, ct);
        }
    }
}
