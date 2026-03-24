using Shouldly;
using XCom2ConfigParser2.Core;
using XCom2ConfigParser2.Parser;

namespace XCom2ConfigParser2.Tests.Parser;

public class LineSplitterTests
{
    [Fact]
    public void Split_SingleLine_NoContinuation_ReturnsOneLine()
    {
        var text = "Hello World";

        var lines = LineSplitter.Split(text);

        lines.Count.ShouldBe(1);
        lines[0].Extract(text).ShouldBe("Hello World");
        lines[0].HasContinuation.ShouldBeFalse();
        lines[0].IsContinuation.ShouldBeFalse();
    }

    [Fact]
    public void Split_MultipleLines_ReturnsAllLines()
    {
        var text = "Line1\r\nLine2\nLine3";

        var lines = LineSplitter.Split(text);

        lines.Count.ShouldBe(3);
        lines[0].Extract(text).ShouldBe("Line1");
        lines[1].Extract(text).ShouldBe("Line2");
        lines[2].Extract(text).ShouldBe("Line3");
    }

    [Fact]
    public void Split_LineWithContinuation_MarksHasContinuation()
    {
        var text = "Line1 \\\\\r\nLine2";

        var lines = LineSplitter.Split(text);

        lines.Count.ShouldBe(2);
        lines[0].HasContinuation.ShouldBeTrue();
        lines[0].Extract(text).ShouldBe("Line1");
    }

    [Fact]
    public void Split_EmptyString_ReturnsEmptyList()
    {
        var text = "";

        var lines = LineSplitter.Split(text);

        lines.ShouldBeEmpty();
    }

    [Fact]
    public void Split_OnlyWhitespace_ReturnsLinesWithWhitespace()
    {
        var text = "   \n\t";

        var lines = LineSplitter.Split(text);

        lines.Count.ShouldBe(2);
    }

    [Fact]
    public void GetMergedLines_Continuation_MergesLines()
    {
        var text = "Line1 \\\\\r\nLine2";
        var lines = LineSplitter.Split(text);

        var merged = LineSplitter.GetMergedLines(text, lines);

        merged.Count.ShouldBe(1);
        merged[0].Text.ShouldContain("Line1");
        merged[0].Text.ShouldContain("Line2");
        merged[0].Text.ShouldBe("Line1  Line2");
    }

    [Fact]
    public void GetMergedLines_TrailingContinuation_SetsErrorFlag()
    {
        var text = "Line1 \\\\";
        var lines = LineSplitter.Split(text);

        var merged = LineSplitter.GetMergedLines(text, lines);

        merged.Count.ShouldBe(1);
        merged[0].HasTrailingContinuation.ShouldBeTrue();
    }

    [Fact]
    public void Split_LineWithContinuationAndTrailingWhitespace_TrimsCorrectly()
    {
        var text = "Line1 \\\\  \r\nLine2";

        var lines = LineSplitter.Split(text);

        lines.Count.ShouldBe(2);
        lines[0].HasContinuation.ShouldBeTrue();
        lines[0].Extract(text).ShouldBe("Line1");
        
        var merged = LineSplitter.GetMergedLines(text, lines);
        merged.Count.ShouldBe(1);
        merged[0].Text.ShouldBe("Line1  Line2");
    }
}
