using Shouldly;
using X2ModCompiler.Core.StructValidation;

namespace X2ModCompiler.Core.Tests.StructValidation;

public class StructFieldTests
{
    [Fact]
    public void StructField_PrimitiveType_IsStructReturnsFalse()
    {
        var field = new StructField { Name = "Count", TypeName = "int" };

        field.IsStruct.ShouldBeFalse();
        field.BaseType.ShouldBe("int");
    }

    [Fact]
    public void StructField_ArrayType_IsStructReturnsFalse()
    {
        var field = new StructField { Name = "Items", TypeName = "int[]" };

        field.IsStruct.ShouldBeFalse();
        field.BaseType.ShouldBe("int");
    }

    [Fact]
    public void StructField_StructType_IsStructReturnsTrue()
    {
        var field = new StructField { Name = "Data", TypeName = "MyStruct" };

        field.IsStruct.ShouldBeTrue();
        field.BaseType.ShouldBe("MyStruct");
    }

    [Fact]
    public void StructField_StructArrayType_IsStructReturnsTrue()
    {
        var field = new StructField { Name = "Items", TypeName = "MyStruct[]" };

        field.IsStruct.ShouldBeTrue();
        field.BaseType.ShouldBe("MyStruct");
    }

    [Fact]
    public void StructField_BoolType_IsStructReturnsFalse()
    {
        var field = new StructField { Name = "Enabled", TypeName = "bool" };

        field.IsStruct.ShouldBeFalse();
        field.BaseType.ShouldBe("bool");
    }

    [Fact]
    public void StructField_NameType_IsStructReturnsFalse()
    {
        var field = new StructField { Name = "Id", TypeName = "name" };

        field.IsStruct.ShouldBeFalse();
        field.BaseType.ShouldBe("name");
    }

    [Fact]
    public void StructField_StringType_IsStructReturnsFalse()
    {
        var field = new StructField { Name = "Description", TypeName = "string" };

        field.IsStruct.ShouldBeFalse();
        field.BaseType.ShouldBe("string");
    }

    [Fact]
    public void StructField_FloatType_IsStructReturnsFalse()
    {
        var field = new StructField { Name = "Weight", TypeName = "float" };

        field.IsStruct.ShouldBeFalse();
        field.BaseType.ShouldBe("float");
    }
}
