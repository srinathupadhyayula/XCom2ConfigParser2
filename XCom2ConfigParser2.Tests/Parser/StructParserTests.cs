using Shouldly;
using XCom2ConfigParser2.Parser;

namespace XCom2ConfigParser2.Tests.Parser;

public class StructParserTests
{
    [Fact]
    public void Parse_EmptyStruct_ReturnsEmptyValue()
    {
        var result = StructParser.Parse("()");
        result.ShouldBeOfType<EmptyValue>();
    }

    [Fact]
    public void Parse_SimpleStruct_ReturnsStructWithChildren()
    {
        var result = StructParser.Parse("(Name=Value)");

        var structValue = result.ShouldBeOfType<StructValue>();
        structValue.Children.Count.ShouldBe(1);
        structValue.Children[0].Name.ShouldBe("Name");
        structValue.Children[0].Value.ShouldBeOfType<TerminalValue>().Text.ShouldBe("Value");
    }

    [Fact]
    public void Parse_StructWithMultipleProperties_ReturnsAllChildren()
    {
        var result = StructParser.Parse("(Prop1=Value1, Prop2=Value2, Prop3=Value3)");

        var structValue = result.ShouldBeOfType<StructValue>();
        structValue.Children.Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_StructWithIndex_ReturnsIndexedProperty()
    {
        var result = StructParser.Parse("(Prop[0]=Value)");

        var structValue = result.ShouldBeOfType<StructValue>();
        structValue.Children[0].Name.ShouldBe("Prop");
        structValue.Children[0].Index.ShouldBe((uint?)0);
    }

    [Fact]
    public void Parse_NestedStruct_ReturnsNestedStructure()
    {
        var result = StructParser.Parse("(Outer=(Inner=Value))");

        var outerStruct = result.ShouldBeOfType<StructValue>();
        outerStruct.Children.Count.ShouldBe(1);
        outerStruct.Children[0].Value.ShouldBeOfType<StructValue>();
    }

    [Fact]
    public void Parse_SimpleArray_ReturnsArrayWithElements()
    {
        var result = StructParser.Parse("(Item1, Item2, Item3)");

        var arrayValue = result.ShouldBeOfType<ArrayValue>();
        arrayValue.Elements.Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_ArrayOfStructs_ReturnsArrayWithStructElements()
    {
        var result = StructParser.Parse("((A=1), (A=2), (A=3))");

        var arrayValue = result.ShouldBeOfType<ArrayValue>();
        arrayValue.Elements.Count.ShouldBe(3);
        arrayValue.Elements[0].ShouldBeOfType<StructValue>();
    }

    [Fact]
    public void Parse_QuotedString_ReturnsTerminalWithQuotes()
    {
        var result = StructParser.Parse("(Name=\"Hello World\")");

        var structValue = result.ShouldBeOfType<StructValue>();
        structValue.Children[0].Value.ShouldBeOfType<TerminalValue>().Text.ShouldBe("\"Hello World\"");
    }

    [Fact]
    public void Parse_BooleanValue_ReturnsTerminal()
    {
        var result = StructParser.Parse("(Enabled=true)");

        var structValue = result.ShouldBeOfType<StructValue>();
        structValue.Children[0].Value.ShouldBeOfType<TerminalValue>().Text.ShouldBe("true");
    }

    [Fact]
    public void Parse_NumericValue_ReturnsTerminal()
    {
        var result = StructParser.Parse("(Count=42)");

        var structValue = result.ShouldBeOfType<StructValue>();
        structValue.Children[0].Value.ShouldBeOfType<TerminalValue>().Text.ShouldBe("42");
    }

    [Fact]
    public void Parse_InvalidStruct_ThrowsParseException()
    {
        Should.Throw<ParseException>(() => StructParser.Parse("(Name=Value"));
    }

    [Fact]
    public void TryParse_NotStruct_ReturnsNull()
    {
        var result = StructParser.TryParse("NotAStruct");
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParse_InvalidStruct_ReturnsNull()
    {
        var result = StructParser.TryParse("(Invalid");
        result.ShouldBeNull();
    }

    [Fact]
    public void Parse_ArrayWithTrailingComma_ShouldStop()
    {
        // This validates that the parser correctly handles trailing commas in arrays,
        // which would occur after merging a multi-line KVP like:
        // MyArray = (Val1, \\\n  )
        // Which translates to: (Val1,   )
        
        var result = StructParser.Parse("(\"SpectrumMECFollowers\",  )");
        var arrayValue = result.ShouldBeOfType<ArrayValue>();
        arrayValue.Elements.Count.ShouldBe(1);
        arrayValue.Elements[0].ShouldBeOfType<TerminalValue>().Text.ShouldBe("\"SpectrumMECFollowers\"");
    }
}
