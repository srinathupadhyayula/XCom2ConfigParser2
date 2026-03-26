using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using X2ModCompiler.Compilation;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;
using Xunit;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Tests;

public class ShaderPrecompilerTests : TestBase
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileMirrorParity _fileMirror;
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;
    private readonly ShaderPrecompiler _precompiler;

    public ShaderPrecompilerTests()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _fileMirror = Substitute.For<IFileMirrorParity>();
        
        _precompiler = new ShaderPrecompiler(_processRunner, _fileMirror, _loggerFactory, Substitute.For<ILogger<ShaderPrecompiler>>());
    }

    [Fact]
    public async Task PrecompileAsync_Skips_WhenNoContentFiles()
    {
        await using var temp = new TempDirectory();
        // Arrange: Empty source directory
        var options = CreateOptions(temp.Path);
        var srcContent = Path.Combine(options.ModSrcRoot, "Content");
        Directory.CreateDirectory(srcContent);
        
        // Act
        await _precompiler.PrecompileAsync(options, TestContext.Current.CancellationToken);

        // Assert
        await _processRunner.DidNotReceive().RunProcessAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string?>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrecompileAsync_CopiesFromCache_WhenCacheIsUpToDate()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var options = CreateOptions(temp.Path);
        var srcContent = Path.Combine(options.ModSrcRoot, "Content");
        Directory.CreateDirectory(srcContent);
        var upkFile = Path.Combine(srcContent, "Asset.upk");
        File.WriteAllText(upkFile, "dummy content");
        
        var cacheDir = options.BuildCachePath;
        Directory.CreateDirectory(cacheDir);
        var cachedFile = Path.Combine(cacheDir, "TestMod_ModShaderCache.upk");
        File.WriteAllText(cachedFile, "cached shader");
        
        // Ensure cache is newer
        File.SetLastWriteTime(cachedFile, DateTime.Now.AddHours(1));

        // Act
        await _precompiler.PrecompileAsync(options, TestContext.Current.CancellationToken);

        // Assert
        await _processRunner.DidNotReceive().RunProcessAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string?>>(), Arg.Any<CancellationToken>());
        await _fileMirror.Received(1).CopyAsync(cachedFile, Arg.Any<string>(), true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PrecompileAsync_RunsCmdlet_WhenContentIsNewer()
    {
        await using var temp = new TempDirectory();
        // Arrange
        var options = CreateOptions(temp.Path);
        var srcContent = Path.Combine(options.ModSrcRoot, "Content");
        Directory.CreateDirectory(srcContent);
        var upkFile = Path.Combine(srcContent, "Asset.upk");
        File.WriteAllText(upkFile, "dummy content");
        File.SetLastWriteTime(upkFile, DateTime.Now.AddHours(2));

        var cacheDir = options.BuildCachePath;
        Directory.CreateDirectory(cacheDir);
        var cachedFile = Path.Combine(cacheDir, "TestMod_ModShaderCache.upk");
        File.WriteAllText(cachedFile, "old cached shader");
        File.SetLastWriteTime(cachedFile, DateTime.Now.AddHours(1));

        _processRunner.RunProcessAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Action<string?>>(), Arg.Any<CancellationToken>())
                      .Returns(Task.FromResult(0));

        // Act
        await _precompiler.PrecompileAsync(options, TestContext.Current.CancellationToken);

        // Assert
        string expectedArgs = "precompileshaders -nopause platform=pc_sm4 DLC=TestMod";
        await _processRunner.Received(1).RunProcessAsync(Arg.Any<string>(), expectedArgs, Arg.Any<Action<string?>>(), Arg.Any<CancellationToken>());
        
        // Should copy result back to cache
        string stagingShaderCache = Path.Combine(options.StagingPath, "Content", "TestMod_ModShaderCache.upk");
        await _fileMirror.Received(1).CopyAsync(stagingShaderCache, cachedFile, true, Arg.Any<CancellationToken>());
    }

    private BuildOptions CreateOptions(string tempPath)
    {
        return new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = Path.Combine(tempPath, "TestMod"),
            SdkPath = Path.Combine(tempPath, "SDK"),
            BuildCachePathOverride = Path.Combine(tempPath, "Cache")
        };
    }
}
