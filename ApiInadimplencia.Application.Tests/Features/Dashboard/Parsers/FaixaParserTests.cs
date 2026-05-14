using ApiInadimplencia.Application.Features.Dashboard.Parsers;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Dashboard.Parsers;

public class FaixaParserTests
{
    [Fact]
    public void Parse_ValidValue_ReturnsValue()
    {
        // Arrange & Act
        var result = FaixaParser.Parse("0-30");

        // Assert
        Assert.Equal("0-30", result);
    }

    [Fact]
    public void Parse_Null_ReturnsAll()
    {
        // Arrange & Act
        var result = FaixaParser.Parse(null);

        // Assert
        Assert.Equal("all", result);
    }

    [Fact]
    public void Parse_Empty_ReturnsAll()
    {
        // Arrange & Act
        var result = FaixaParser.Parse(string.Empty);

        // Assert
        Assert.Equal("all", result);
    }

    [Fact]
    public void Parse_Whitespace_ReturnsAll()
    {
        // Arrange & Act
        var result = FaixaParser.Parse("   ");

        // Assert
        Assert.Equal("all", result);
    }

    [Fact]
    public void Parse_InvalidValue_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => FaixaParser.Parse("invalid"));
    }

    [Theory]
    [InlineData("0-30")]
    [InlineData("31-60")]
    [InlineData("61-90")]
    [InlineData("91-120")]
    [InlineData("121-180")]
    [InlineData("181+")]
    [InlineData("0-30-dias")]
    [InlineData("31-60-dias")]
    [InlineData("61-90-dias")]
    [InlineData("91-120-dias")]
    [InlineData("121-180-dias")]
    [InlineData("181+-dias")]
    public void Parse_AllowedValues_AcceptsValue(string value)
    {
        // Arrange & Act
        var result = FaixaParser.Parse(value);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void Allowed_ContainsAllExpectedValues()
    {
        // Assert
        Assert.Contains("0-30", FaixaParser.Allowed);
        Assert.Contains("31-60", FaixaParser.Allowed);
        Assert.Contains("61-90", FaixaParser.Allowed);
        Assert.Contains("91-120", FaixaParser.Allowed);
        Assert.Contains("121-180", FaixaParser.Allowed);
        Assert.Contains("181+", FaixaParser.Allowed);
    }
}
