using Xunit;
using X2ModCompiler.Compilation;
using Shouldly;
using System.Collections.Generic;

namespace X2ModCompiler.Tests.Configuration;

public class BuildResultTests
{
    [Fact]
    public void Constructor_WithConfigCounts_StoresValues()
    {
        var result = new BuildResult(
            Success: true,
            Duration: TimeSpan.FromSeconds(1),
            OutputPaths: new List<string>(),
            Errors: new List<string>(),
            Timings: new List<BuildTimingRecord>(),
            ConfigErrorCount: 2,
            ConfigWarningCount: 3);
        
        result.ConfigErrorCount.ShouldBe(2);
        result.ConfigWarningCount.ShouldBe(3);
    }

    [Fact]
    public void Constructor_WithoutConfigCounts_DefaultsToZero()
    {
        var result = new BuildResult(
            Success: true,
            Duration: TimeSpan.FromSeconds(1),
            OutputPaths: new List<string>(),
            Errors: new List<string>(),
            Timings: new List<BuildTimingRecord>());
        
        result.ConfigErrorCount.ShouldBe(0);
        result.ConfigWarningCount.ShouldBe(0);
    }
}
