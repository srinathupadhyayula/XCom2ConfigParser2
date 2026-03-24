using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Utilities;

namespace XCom2ModCompiler.Compilation;

public class ShaderPrecompiler
{
    private readonly IProcessRunner _runner;
    private readonly IFileMirrorParity _mirror;
    private readonly ILogger<ShaderPrecompiler> _logger;

    public ShaderPrecompiler(IProcessRunner runner, IFileMirrorParity mirror, ILogger<ShaderPrecompiler> logger)
    {
        _runner = runner;
        _mirror = mirror;
        _logger = logger;
    }

    public virtual async Task PrecompileAsync(BuildOptions options, CancellationToken ct)
    {
        _logger.LogInformation("Checking if shader precompilation is required...");

        var contentDir = Path.Combine(options.ModSrcRoot, "Content");
        if (!Directory.Exists(contentDir))
        {
            _logger.LogInformation("No Content directory found, skipping shader precompilation.");
            return;
        }

        var upkFiles = Directory.GetFiles(contentDir, "*.upk", SearchOption.AllDirectories)
                                .Select(f => new FileInfo(f))
                                .ToList();

        if (!upkFiles.Any())
        {
            _logger.LogInformation("No content files, skipping PrecompileShaders.");
            return;
        }

        var shaderCacheName = $"{options.ModNameCanonical}_ModShaderCache.upk";
        var cachedShaderCachePath = Path.Combine(options.BuildCachePath, shaderCacheName);
        var stagingShaderCachePath = Path.Combine(options.StagingPath, "Content", shaderCacheName);

        bool needsPrecompile = false;
        if (!File.Exists(cachedShaderCachePath))
        {
            needsPrecompile = true;
            _logger.LogInformation("No cached shader cache found, forcing precompilation.");
        }
        else
        {
            var cacheInfo = new FileInfo(cachedShaderCachePath);
            foreach (var file in upkFiles)
            {
                if (file.LastWriteTime > cacheInfo.LastWriteTime || file.CreationTime > cacheInfo.LastWriteTime)
                {
                    needsPrecompile = true;
                    _logger.LogInformation("Content file {FileName} is newer than cached shader cache, forcing precompilation.", file.Name);
                    break;
                }
            }
        }

        if (needsPrecompile)
        {
            _logger.LogInformation("Precompiling shaders...");
            var args = $"precompileshaders -nopause platform=pc_sm4 DLC={options.ModNameCanonical}";

            var receiver = new PassthroughReceiver();
            receiver.ProcessDescription = "precompiling shaders";

            int exitCode = await _runner.RunProcessAsync(options.CommandletPath, args, receiver.ParseLine, ct);
            receiver.Finish(exitCode);

            if (exitCode == 0)
            {
                _logger.LogInformation("Generated Shader Cache.");

                if (!Directory.Exists(options.BuildCachePath))
                {
                    Directory.CreateDirectory(options.BuildCachePath);
                }
                await _mirror.CopyAsync(stagingShaderCachePath, cachedShaderCachePath, true, ct);
            }
        }
        else
        {
            _logger.LogInformation("No reason to precompile shaders, using existing");
            var stagingContentDir = Path.Combine(options.StagingPath, "Content");
            if (!Directory.Exists(stagingContentDir))
            {
                Directory.CreateDirectory(stagingContentDir);
            }
            await _mirror.CopyAsync(cachedShaderCachePath, stagingShaderCachePath, true, ct);
        }
    }
}
