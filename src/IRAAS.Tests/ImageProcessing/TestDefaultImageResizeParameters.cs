using System.Collections.Generic;
using IRAAS.ImageProcessing;
using NUnit.Framework;
using PeanutButter.DuckTyping.Extensions;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace IRAAS.Tests.ImageProcessing;

[TestFixture]
public class TestDefaultImageResizeParameters: TestBase
{
    [Test]
    public void ShouldDuckDefaults()
    {
        // Arrange
        var expectedResizeMode = GetRandom<ResizeMode>();
        var expectedJpegEncodingColor = GetRandom<JpegEncodingColor>();
        var expectedJpegColorType = GetRandom<JpegEncodingColor>();
        var expectedPngColorType = GetRandom<PngColorType>();
        var expectedPngFilterMethod = GetRandom<PngFilterMethod>();
        var expectedGifColorTableMode = GetRandom<GifColorTableMode>();
        var expectedQuantizer = GetRandomString();
        var expectedReplaceTransparencyWith = GetRandomString();
        var expectedFormat = GetRandomString();
        var expectedWidth = GetRandomInt();
        var expectedHeight = GetRandomInt();
        var expectedGamma = GetRandomFloat();
        var expectedTransparencyThreshold = (byte) GetRandomInt(0, 255);
        var expectedBitDepth = GetRandomFrom([8, 16, 24, 32]);
        var expectedCompressionLevel = GetRandomInt(1, 10);
        var expectedSampler = GetRandomString();
        var expectedMaxColors = GetRandomInt(1);
        var expectedDither = GetRandomBoolean();
        var expectedDevicePixelRatio = GetRandomDecimal(1, 2);
        var expectedQuality = GetRandomInt(60, 100);
        var defaults = new Dictionary<string, string>()
        {
            ["ReplaceTransparencyWith"] = expectedReplaceTransparencyWith,
            ["Format"] = expectedFormat,
            ["Quality"] = $"{expectedQuality}",
            ["Width"] = $"{expectedWidth}",
            ["Height"] = $"{expectedHeight}",
            ["ResizeMode"] = $"{expectedResizeMode}",
            ["JpegColorType"] = $"{expectedJpegColorType}",
            ["JpegEncodingColor"] = $"{expectedJpegEncodingColor}",
            ["Gamma"] = $"{expectedGamma}",
            ["Quantizer"] = expectedQuantizer,
            ["TransparencyThreshold"] = $"{expectedTransparencyThreshold}",
            ["BitDepth"] = $"{expectedBitDepth}",
            ["PngColorType"] = $"{expectedPngColorType}",
            ["CompressionLevel"] = $"{expectedCompressionLevel}",
            ["PngFilterMethod"] = $"{expectedPngFilterMethod}",
            ["Sampler"] = expectedSampler,
            ["GifColorTableMode"] = $"{expectedGifColorTableMode}",
            ["MaxColors"] = $"{expectedMaxColors}",
            ["Dither"] = $"{expectedDither}",
            ["DevicePixelRatio"] = $"{expectedDevicePixelRatio}"
        };

        // Act
        var result = defaults.FuzzyDuckAs<IDefaultImageResizeParameters>(throwOnError: true);
        // Assert
        Expect(result.ReplaceTransparencyWith)
            .To.Equal(expectedReplaceTransparencyWith);
        Expect(result.Format)
            .To.Equal(expectedFormat);
        Expect(result.Quality)
            .To.Equal(expectedQuality);
        Expect(result.Width)
            .To.Equal(expectedWidth);
        Expect(result.Height)
            .To.Equal(expectedHeight);
        Expect(result.ResizeMode)
            .To.Equal(expectedResizeMode);
        Expect(result.JpegColorType)
            .To.Equal(expectedJpegColorType);
        Expect(result.JpegEncodingColor)
            .To.Equal(expectedJpegEncodingColor);
        Expect(result.Gamma)
            .To.Equal(expectedGamma);
        Expect(result.Quantizer)
            .To.Equal(expectedQuantizer);
        Expect(result.TransparencyThreshold)
            .To.Equal(expectedTransparencyThreshold);
        Expect(result.BitDepth)
            .To.Equal(expectedBitDepth);
        Expect(result.PngColorType)
            .To.Equal(expectedPngColorType);
        Expect(result.CompressionLevel)
            .To.Equal(expectedCompressionLevel);
        Expect(result.PngFilterMethod)
            .To.Equal(expectedPngFilterMethod);
        Expect(result.Sampler)
            .To.Equal(expectedSampler);
        Expect(result.GifColorTableMode)
            .To.Equal(expectedGifColorTableMode);
        Expect(result.MaxColors)
            .To.Equal(expectedMaxColors);
        Expect(result.Dither)
            .To.Equal(expectedDither);
        Expect(result.DevicePixelRatio)
            .To.Equal(expectedDevicePixelRatio);
    }
}