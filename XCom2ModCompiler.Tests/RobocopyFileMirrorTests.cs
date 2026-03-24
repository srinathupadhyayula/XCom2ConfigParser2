using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using XCom2ModCompiler.Utilities;

namespace XCom2ModCompiler.Tests;

public class RobocopyFileMirrorTests
{
    private readonly Mock<IProcessRunner> _runnerMock;
    private readonly RobocopyFileMirror _mirror;

    public RobocopyFileMirrorTests()
    {
        _runnerMock = new Mock<IProcessRunner>();
        _mirror = new RobocopyFileMirror(_runnerMock.Object);
    }

    [Fact]
    public async Task MirrorAsync_UsesCorrectRobocopyArguments()
    {
        var src = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString());
        Directory.CreateDirectory(src);
        
        try
        {
            _runnerMock.Setup(r => r.RunProcessAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(1); // 1 = One or more files were copied successfully (Robocopy valid exit code)

            await _mirror.MirrorAsync(src, @"D:\Dest", "*.uc");

            string expectedArgs = $@"""{src}"" ""D:\Dest"" ""*.uc"" /MIR /NFL /NDL /NJH /NJS /nc /ns /np";
            
            _runnerMock.Verify(r => r.RunProcessAsync("robocopy.exe", expectedArgs, null, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Directory.Delete(src, true);
        }
    }

    [Fact]
    public async Task MirrorAsync_ExcludesFileWhenSpecified()
    {
        var src = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString());
        Directory.CreateDirectory(src);

        try
        {
            _runnerMock.Setup(r => r.RunProcessAsync(It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(3);

            await _mirror.MirrorAsync(src, @"D:\Dest", "*.*", new[] { "ContentOptions.json" }, default);

            string expectedArgs = $@"""{src}"" ""D:\Dest"" ""*.*"" /MIR /NFL /NDL /NJH /NJS /nc /ns /np /XF ""ContentOptions.json""";
            
            _runnerMock.Verify(r => r.RunProcessAsync("robocopy.exe", expectedArgs, null, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Directory.Delete(src, true);
        }
    }

    [Fact]
    public async Task DeleteAsync_ExecutesDirectoryDelete_WhenDirectoryExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            await _mirror.DeleteAsync(tempDir, true);
            Assert.False(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
