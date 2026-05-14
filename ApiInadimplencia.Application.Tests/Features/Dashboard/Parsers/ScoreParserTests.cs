using ApiInadimplencia.Application.Features.Dashboard.Parsers;
using Xunit;

namespace ApiInadimplencia.Application.Tests.Features.Dashboard.Parsers;

public class ScoreParserTests
{
    [Fact]
    public void Parse_ValidValue_ReturnsValue()
    {
        // Arrange & Act
        var result = ScoreParser.Parse("A");

        // Assert
        Assert.Equal("A", result);
    }

    [Fact]
    public void Parse_Null_ReturnsAll()
    {
        // Arrange & Act
        var result = ScoreParser.Parse(null);

        // Assert
        Assert.Equal("all", result);
    }

    [Fact]
    public void Parse_Empty_ReturnsAll()
    {
        // Arrange & Act
        var result = ScoreParser.Parse(string.Empty);

        // Assert
        Assert.Equal("all", result);
    }

    [Fact]
    public void Parse_Whitespace_ReturnsAll()
    {
        // Arrange & Act
        var result = ScoreParser.Parse("   ");

        // Assert
        Assert.Equal("all", result);
    }

    [Fact]
    public void Parse_InvalidValue_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => ScoreParser.Parse("invalid"));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    [InlineData("D")]
    [InlineData("E")]
    [InlineData("F")]
    [InlineData("G")]
    [InlineData("H")]
    [InlineData("A-B")]
    [InlineData("C-D")]
    [InlineData("E-F")]
    [InlineData("G-H")]
    [InlineData("alto")]
    [InlineData("medio")]
    [InlineData("baixo")]
    [InlineData("all")]
    public void Parse_AllowedValues_AcceptsValue(string value)
    {
        // Arrange & Act
        var result = ScoreParser.Parse(value);

        // Assert
        Assert.Equal(value, result);
    }

    [Fact]
    public void Allowed_ContainsAllExpectedValues()
    {
        // Assert
        Assert.Contains("A", ScoreParser.Allowed);
        Assert.Contains("B", ScoreParser.Allowed);
        Assert.Contains("C", ScoreParser.Allowed);
        Assert.Contains("D", ScoreParser.Allowed);
        Assert.Contains("E", ScoreParser.Allowed);
        Assert.Contains("F", ScoreParser.Allowed);
        Assert.Contains("G", ScoreParser.Allowed);
        Assert.Contains("H", ScoreParser.Allowed);
        Assert.Contains("A-B", ScoreParser.Allowed);
        Assert.Contains("C-D", ScoreParser.Allowed);
        Assert.Contains("E-F", ScoreParser.Allowed);
        Assert.Contains("G-H", ScoreParser.Allowed);
        Assert.Contains("alto", ScoreParser.Allowed);
        Assert.Contains("medio", ScoreParser.Allowed);
        Assert.Contains("baixo", ScoreParser.Allowed);
        Assert.Contains("all", ScoreParser.Allowed);
    }
}
