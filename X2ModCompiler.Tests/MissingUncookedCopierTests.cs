using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using X2ModCompiler.Configuration;
using X2ModCompiler.Utilities;
using Xunit;
using X2ModCompiler.Tests.Shared;

namespace X2ModCompiler.Tests;

public class MissingUncookedCopierTests : TestBase
{
    private readonly IFileMirrorParity _mirror;
    private readonly ILogger<MissingUncookedCopier> _logger;
    private readonly MissingUncookedCopier _copier;
    private readonly BuildOptions _options;
    private readonly ContentOptions _contentOptions;

    public MissingUncookedCopierTests()
    {
        _mirror = Substitute.For<IFileMirrorParity>();
        _logger = Substitute.For<ILogger<MissingUncookedCopier>>();
        _copier = new MissingUncookedCopier(_mirror, _logger);
        
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
        await _copier.CopyMissingAsync(_options, _contentOptions, TestContext.Current.CancellationToken);

        // Assert
        string gameCookedDir = Path.Combine(_options.GamePath, "XComGame", "CookedPCConsole");
        string stagingCookedDir = Path.Combine(_options.StagingPath, "CookedPCConsole");

        await _mirror.Received(1).CopyAsync(
            Path.Combine(gameCookedDir, "PackageA.upk"),
            Path.Combine(stagingCookedDir, "PackageA.upk"),
            true,
            Arg.Any<CancellationToken>());

        await _mirror.Received(1).CopyAsync(
            Path.Combine(gameCookedDir, "PackageB.upk"),
            Path.Combine(stagingCookedDir, "PackageB.upk"),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CopyMissingAsync_Skips_WhenListIsEmpty()
    {
        // Arrange
        var emptyContent = new ContentOptions();

        // Act
        await _copier.CopyMissingAsync(_options, emptyContent, TestContext.Current.CancellationToken);

        // Assert
        await _mirror.DidNotReceiveWithAnyArgs().CopyAsync(default!, default!, default, TestContext.Current.CancellationToken);
    }
}
