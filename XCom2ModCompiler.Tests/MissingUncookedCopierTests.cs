using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using XCom2ModCompiler.Configuration;
using XCom2ModCompiler.Utilities;
using Xunit;

namespace XCom2ModCompiler.Tests;

public class MissingUncookedCopierTests
{
    private readonly Mock<IFileMirrorParity> _mirrorMock;
    private readonly Mock<ILogger<MissingUncookedCopier>> _loggerMock;
    private readonly MissingUncookedCopier _copier;
    private readonly BuildOptions _options;
    private readonly ContentOptions _contentOptions;

    public MissingUncookedCopierTests()
    {
        _mirrorMock = new Mock<IFileMirrorParity>();
        _loggerMock = new Mock<ILogger<MissingUncookedCopier>>();
        _copier = new MissingUncookedCopier(_mirrorMock.Object, _loggerMock.Object);
        
        _options = new BuildOptions
        {
            GamePath = @"C:\Game",
            ProjectRoot = @"C:\Src\TestMod",
            SdkPath = @"C:\SDK",
            ModName = "TestMod"
        };
        
        _contentOptions = new ContentOptions
        {
            MissingUncooked = new List<string> { "PackageA.upk", "PackageB.upk" }
        };
    }

    [Fact]
    public async Task CopyMissingAsync_CopiesSpecifiedFiles()
    {
        // Act
        await _copier.CopyMissingAsync(_options, _contentOptions, CancellationToken.None);

        // Assert
        string gameCookedDir = Path.Combine(_options.GamePath, "XComGame", "CookedPCConsole");
        string stagingCookedDir = Path.Combine(_options.StagingPath, "CookedPCConsole");

        _mirrorMock.Verify(m => m.CopyAsync(
            Path.Combine(gameCookedDir, "PackageA.upk"),
            Path.Combine(stagingCookedDir, "PackageA.upk"),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        _mirrorMock.Verify(m => m.CopyAsync(
            Path.Combine(gameCookedDir, "PackageB.upk"),
            Path.Combine(stagingCookedDir, "PackageB.upk"),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CopyMissingAsync_Skips_WhenListIsEmpty()
    {
        // Arrange
        var emptyContent = new ContentOptions();

        // Act
        await _copier.CopyMissingAsync(_options, emptyContent, CancellationToken.None);

        // Assert
        _mirrorMock.Verify(m => m.CopyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
