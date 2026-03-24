using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using XCom2ModCompiler.Compilation;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Utilities;
using Xunit;

namespace XCom2ModCompiler.Tests;

public class ShaderPrecompilerTests
{
    private readonly Mock<IProcessRunner> _processRunnerMock;
    private readonly Mock<IFileMirror> _fileMirrorMock;
    private readonly Mock<ILogger<ShaderPrecompiler>> _loggerMock;
    private readonly ShaderPrecompiler _precompiler;
    private readonly BuildOptions _options;

    public ShaderPrecompilerTests()
    {
        _processRunnerMock = new Mock<IProcessRunner>();
        _fileMirrorMock = new Mock<IFileMirror>();
        _loggerMock = new Mock<ILogger<ShaderPrecompiler>>();
        
        _options = new BuildOptions
        {
            ModName = "TestMod",
            ProjectRoot = @"C:\Src\TestMod",
            SdkPath = @"C:\SDK",
            BuildCachePathOverride = @"C:\Cache"
        };
        
        _precompiler = new ShaderPrecompiler(_processRunnerMock.Object, _fileMirrorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task PrecompileAsync_Skips_WhenNoContentFiles()
    {
        // Arrange: Empty source directory
        var srcContent = Path.Combine(_options.ModSrcRoot, "Content");
        Directory.CreateDirectory(srcContent);
        
        try {
            // Act
            await _precompiler.PrecompileAsync(_options, CancellationToken.None);

            // Assert
            _processRunnerMock.Verify(r => r.RunProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string?>>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally {
            Directory.Delete(_options.ProjectRoot, true);
        }
    }

    [Fact]
    public async Task PrecompileAsync_CopiesFromCache_WhenCacheIsUpToDate()
    {
        // Arrange
        var srcContent = Path.Combine(_options.ModSrcRoot, "Content");
        Directory.CreateDirectory(srcContent);
        var upkFile = Path.Combine(srcContent, "Asset.upk");
        File.WriteAllText(upkFile, "dummy content");
        
        var cacheDir = _options.BuildCachePath;
        Directory.CreateDirectory(cacheDir);
        var cachedFile = Path.Combine(cacheDir, "TestMod_ModShaderCache.upk");
        File.WriteAllText(cachedFile, "cached shader");
        
        // Ensure cache is newer
        File.SetLastWriteTime(cachedFile, DateTime.Now.AddHours(1));

        try {
            // Act
            await _precompiler.PrecompileAsync(_options, CancellationToken.None);

            // Assert
            _processRunnerMock.Verify(r => r.RunProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string?>>(), It.IsAny<CancellationToken>()), Times.Never);
            _fileMirrorMock.Verify(m => m.CopyAsync(cachedFile, It.IsAny<string>(), true, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally {
            Directory.Delete(_options.ProjectRoot, true);
            Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public async Task PrecompileAsync_RunsCmdlet_WhenContentIsNewer()
    {
        // Arrange
        var srcContent = Path.Combine(_options.ModSrcRoot, "Content");
        Directory.CreateDirectory(srcContent);
        var upkFile = Path.Combine(srcContent, "Asset.upk");
        File.WriteAllText(upkFile, "dummy content");
        File.SetLastWriteTime(upkFile, DateTime.Now.AddHours(2));

        var cacheDir = _options.BuildCachePath;
        Directory.CreateDirectory(cacheDir);
        var cachedFile = Path.Combine(cacheDir, "TestMod_ModShaderCache.upk");
        File.WriteAllText(cachedFile, "old cached shader");
        File.SetLastWriteTime(cachedFile, DateTime.Now.AddHours(1));

        _processRunnerMock.Setup(r => r.RunProcessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Action<string?>>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync(0);

        try {
            // Act
            await _precompiler.PrecompileAsync(_options, CancellationToken.None);

            // Assert
            string expectedArgs = "precompileshaders -nopause platform=pc_sm4 DLC=TestMod";
            _processRunnerMock.Verify(r => r.RunProcessAsync(It.IsAny<string>(), expectedArgs, It.IsAny<Action<string?>>(), It.IsAny<CancellationToken>()), Times.Once);
            
            // Should copy result back to cache
            string stagingShaderCache = Path.Combine(_options.StagingPath, "Content", "TestMod_ModShaderCache.upk");
            _fileMirrorMock.Verify(m => m.CopyAsync(stagingShaderCache, cachedFile, true, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally {
            Directory.Delete(_options.ProjectRoot, true);
            Directory.Delete(cacheDir, true);
        }
    }
}
