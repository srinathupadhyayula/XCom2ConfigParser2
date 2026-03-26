using X2ModCompiler.Configuration;
using X2ModCompiler.Exceptions;

namespace X2ModCompiler.Cooking;

/// <summary>
/// Verifies SDK environment and project configuration for asset cooking.
/// Extracted from ModAssetsCookStep to improve testability and separation of concerns.
/// </summary>
public class SdkEnvironmentVerifier
{
    private readonly BuildOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SdkEnvironmentVerifier"/> class.
    /// </summary>
    /// <param name="options">The build options containing project and SDK paths.</param>
    public SdkEnvironmentVerifier(BuildOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Verifies that ContentForCook directory exists.
    /// </summary>
    /// <exception cref="BuildFailureException">Thrown if directory is missing.</exception>
    public void VerifyContentForCookExists()
    {
        var contentForCookPath = System.IO.Path.Combine(_options.ModSrcRoot, "ContentForCook");
        if (!System.IO.Directory.Exists(contentForCookPath))
        {
            throw new BuildFailureException("Asset cooking", 1);
        }
    }

    /// <summary>
    /// Verifies that SDK ContentMods directory is empty or doesn't exist.
    /// </summary>
    /// <exception cref="Exception">Thrown if directory is not empty.</exception>
    public void VerifySdkContentModsDirectoryEmpty()
    {
        var sdkContentModsOurDir = System.IO.Path.Combine(_options.SdkPath, "XComGame", "Content", "Mods", _options.ModNameCanonical);
        if (System.IO.Directory.Exists(sdkContentModsOurDir))
        {
            if (System.IO.Directory.GetFiles(sdkContentModsOurDir, "*", System.IO.SearchOption.AllDirectories).Any())
            {
                throw new Exception($"{sdkContentModsOurDir} is already in use (not empty)");
            }
        }
    }

    /// <summary>
    /// Verifies that shipped GPCD exists.
    /// </summary>
    /// <exception cref="Exception">Thrown if file is missing.</exception>
    public void VerifyShippedGpcdExists()
    {
        var shippedGpcdPath = System.IO.Path.Combine(_options.SdkPath, "XComGame", "CookedPCConsole", "GlobalPersistentCookerData.upk");
        if (!System.IO.File.Exists(shippedGpcdPath))
        {
            throw new Exception($"{shippedGpcdPath} does not exist. Please verify your SDK is configured correctly");
        }
    }

    /// <summary>
    /// Performs all SDK environment verifications in sequence.
    /// </summary>
    /// <exception cref="Exception">Thrown if any verification fails.</exception>
    public void VerifyAll()
    {
        VerifyContentForCookExists();
        VerifySdkContentModsDirectoryEmpty();
        VerifyShippedGpcdExists();
    }
}
