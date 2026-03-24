using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XCom2ModCompiler.Configuration;

namespace XCom2ModCompiler.Utilities;

public class MissingUncookedCopier
{
    private readonly IFileMirrorParity _mirror;
    private readonly ILogger<MissingUncookedCopier> _logger;

    public MissingUncookedCopier(IFileMirrorParity mirror, ILogger<MissingUncookedCopier> logger)
    {
        _mirror = mirror;
        _logger = logger;
    }

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
