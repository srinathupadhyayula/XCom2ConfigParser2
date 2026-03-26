using Kokuban;
using Microsoft.Extensions.Logging;
using X2ModCompiler.Configuration;
using System.Text;
using X2ModCompiler.Utilities;

namespace X2ModCompiler.Application.Steps;

/// <summary>
/// Converts localization files from UTF-8 to UTF-16 encoding.
/// This step mimics _ConvertLocalization() from build_common.ps1.
/// 
/// The Unreal Engine localization system expects UTF-16 encoded files.
/// </summary>
public class LocalizationStep : IBuildStep
{
    private readonly ILogger<LocalizationStep> _logger;

    public LocalizationStep(ILogger<LocalizationStep> logger)
    {
        _logger = logger;
    }

    public string Name => "Convert Localization";

    public async Task<bool> ExecuteAsync(BuildOptions options, CancellationToken ct)
    {
        var localizationPath = Path.Combine(options.StagingPath, "Localization");

        if (!Directory.Exists(localizationPath))
        {
            _logger.LogInformation(LogColors.Debug("No Localization folder found - skipping conversion."));
            return true;
        }

        _logger.LogInformation(LogColors.Info("Converting localization files UTF-8 → UTF-16..."));

        var filesConverted = 0;
        var files = Directory.GetFiles(localizationPath, "*.int", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(localizationPath, "*.loc", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Read as UTF-8
                var utf8Content = await File.ReadAllTextAsync(file, Encoding.UTF8, ct);

                // Write as UTF-16 (Unicode)
                await File.WriteAllTextAsync(file, utf8Content, Encoding.Unicode, ct);

                filesConverted++;
                _logger.LogDebug($"Converted: {file}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(LogColors.Warning($"Failed to convert {file}: {ex.Message}"));
            }
        }

        _logger.LogInformation(LogColors.Success($"Converted {filesConverted} localization file(s)."));
        return true;
    }
}
