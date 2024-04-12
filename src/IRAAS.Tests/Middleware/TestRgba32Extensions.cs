using System;
using System.Collections.Generic;
using IRAAS.ImageProcessing;
using NExpect;
using NExpect.Interfaces;
using NExpect.MatcherLogic;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static NExpect.Expectations;

[TestFixture]
public class TestRgba32Extensions
{
    [TestCase("")]
    [TestCase(" ")]
    [TestCase(null)]
    public void ShouldReturnWhiteFor_(string input)
    {
        // Arrange
        // Act
        var result = input.AsRgba32();
        // Assert
        Expect(result)
            .To.Equal(White);
    }

    [Test]
    public void ShouldReturnExpectedColorForValidHex()
    {
        // Arrange
        var hexColor = "#123456";
        var expected = Rgba32.ParseHex(hexColor);
        // Act
        var result = hexColor.AsRgba32();
        // Assert
        Expect(result)
            .To.Equal(expected);
    }

    [Test]
    public void ShouldReturnExpectedColorForValidHexWithAlpha()
    {
        // Arrange
        var hexColor = "#123456FF";
        var expected = Rgba32.ParseHex(hexColor);
        // Act
        var result = hexColor.AsRgba32();
        // Assert
        Expect(result)
            .To.Equal(expected);
    }

    [Test]
    public void ShouldReturnExpectedColorForValidHexWithoutHash()
    {
        // Arrange
        var hexColor = "123456";
        var expected = Rgba32.ParseHex(hexColor);
        // Act
        var result = hexColor.AsRgba32();
        // Assert
        Expect(result)
            .To.Equal(expected);
    }

    [Test]
    public void ShouldReturnWhiteForInvalidHex()
    {
        // Arrange
        var invalid = "#cdefg";
        // Act
        var result = invalid.AsRgba32();
        // Assert
        Expect(result)
            .To.Equal(White);
    }

    public static IEnumerable<(string, Rgba32)> ColorNameTestCases()
    {
        yield return ("White", White);
        yield return ("wHitE", White);
        yield return ("black", Rgba32.ParseHex(Color.Black.ToHex()));
    }

    [TestCaseSource(nameof(ColorNameTestCases))]
    public void ShouldParseColorNamesToo((string name, Rgba32 expected) testCase)
    {
        // Arrange
        var (name, expected) = testCase;
        // Act
        var result = name.AsRgba32();
        // Assert
        Expect(result)
            .To.Equal(expected);
    }
    
    private static Rgba32 White = Rgba32.ParseHex("fff");
}

