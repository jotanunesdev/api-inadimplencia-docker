using ApiInadimplencia.Application.Features.Dashboard.Parsers;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Dashboard.Parsers;

public class QuantidadeParserTests
{
    [Fact]
    public void Parse_ValidValue_ReturnsValue()
    {
        // Arrange & Act
        var result = QuantidadeParser.Parse("5");

        // Assert
        Assert.Equal("5", result);
    }

    [Fact]
    public void Parse_Null_ReturnsAll()
    {
        // Arrange & Act
        var result = QuantidadeParser.Parse(null);

        // Assert
        Assert.Equal("all", result);
    }

    [Fact]
    public void Parse_Empty_ReturnsAll()
    {
        // Arrange & Act
        var result = QuantidadeParser.Parse(string.Empty);

        // Assert
        Assert.Equal("all", result);
    }

    [Fact]
    public void Parse_Whitespace_ReturnsAll()
    {
        // Arrange & Act
        var result = QuantidadeParser.Parse("   ");

        // Assert
        Assert.Equal("all", result);
    }

    [Fact]
    public void Parse_InvalidValue_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => QuantidadeParser.Parse("invalid"));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    [InlineData("5")]
    [InlineData("6")]
    [InlineData("7")]
    [InlineData("8")]
    [InlineData("9")]
    [InlineData("10")]
    [InlineData("1-5")]
    [InlineData("6-10")]
    [InlineData("10+")]
    [InlineData("all")]
    public void Parse_AllowedValues_AcceptsValue(string value)
    {
        // Arrange & Act
        var result = QuantidadeParser.Parse(value);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void Allowed_ContainsAllExpectedValues()
    {
        // Assert
        Assert.Contains("1", QuantidadeParser.Allowed);
        Assert.Contains("5", QuantidadeParser.Allowed);
        Assert.Contains("10", QuantidadeParser.Allowed);
        Assert.Contains("1-5", QuantidadeParser.Allowed);
        Assert.Contains("6-10", QuantidadeParser.Allowed);
        Assert.Contains("10+", QuantidadeParser.Allowed);
        Assert.Contains("all", QuantidadeParser.Allowed);
    }
}
