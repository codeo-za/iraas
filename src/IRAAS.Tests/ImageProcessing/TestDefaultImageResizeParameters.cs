using System.Collections.Generic;
using IRAAS.ImageProcessing;
using NUnit.Framework;
using PeanutButter.DuckTyping.Extensions;
using PeanutButter.Utils;
using PeanutButter.Utils.Dictionaries;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace IRAAS.Tests.ImageProcessing;

[TestFixture]
public class TestDefaultImageResizeParameters : TestBase
{
    [Test]
    public void ShouldDuckDefaults()
    {
        // Arrange
        var expected = GetRandom<IImageResizeParameters>();
        var defaults = GenerateStringStringDictionaryFrom(expected);

        // Act
        var result = DefaultImageResizeParameters.From(defaults);
        // Assert
        Expect(result)
            .To.Deep.Equal(expected);
    }

    [TestFixture]
    public class PerFormatOverrides
    {
        [Test]
        public void ShouldBeAbleToRegisterAndRetrieve()
        {
            // Arrange
            var generalDefaults = GetRandom<IImageResizeParameters>()
                .With(o => o.Sampler = "Wu");
            var defaults = GenerateStringStringDictionaryFrom(generalDefaults);
            var gifDefaults = new Dictionary<string, string>()
            {
                ["Sampler"] = "Bicubic"
            };

            // Act
            var sut = DefaultImageResizeParameters.From(defaults);
            sut.RegisterOverridesFor("gif", gifDefaults);
            var result1 = sut.For("jpg");
            var result2 = sut.For("gif");
            // Assert
            Expect(result1)
                .To.Deep.Equal(generalDefaults);
            var withoutSampler = result2.DuckAs<IImageResizeParametersWithoutSampler>();
            Expect(result2)
                .To.Intersection.Equal(withoutSampler);
            Expect(result2.Sampler)
                .To.Equal("Bicubic");
        }

        public interface IImageResizeParametersWithoutSampler
        {
            string ReplaceTransparencyWith { get; set; }
            string Format { get; set; }

            [Default(85)]
            int Quality { get; set; }

            int? Width { get; set; }
            int? Height { get; set; }
            ResizeMode? ResizeMode { get; set; }
            JpegEncodingColor? JpegColorType { get; set; }
            JpegEncodingColor? JpegEncodingColor { get; set; }
            float? Gamma { get; set; }
            string Quantizer { get; set; }
            byte? TransparencyThreshold { get; set; }
            int? BitDepth { get; set; }
            PngColorType? PngColorType { get; set; }
            int? CompressionLevel { get; set; }
            PngFilterMethod? PngFilterMethod { get; set; }
            GifColorTableMode? GifColorTableMode { get; set; }
            int? MaxColors { get; set; }
            bool? Dither { get; set; }
            decimal DevicePixelRatio { get; set; }
        }
    }

    private static Dictionary<string, string> GenerateStringStringDictionaryFrom(IImageResizeParameters expected)
    {
        var defaults = new Dictionary<string, string>()
        {
            ["ReplaceTransparencyWith"] = expected.ReplaceTransparencyWith,
            ["Format"] = expected.Format,
            ["Quality"] = $"{expected.Quality}",
            ["Width"] = $"{expected.Width}",
            ["Height"] = $"{expected.Height}",
            ["ResizeMode"] = $"{expected.ResizeMode}",
            ["JpegColorType"] = $"{expected.JpegColorType}",
            ["JpegEncodingColor"] = $"{expected.JpegEncodingColor}",
            ["Gamma"] = $"{expected.Gamma}",
            ["Quantizer"] = expected.Quantizer,
            ["TransparencyThreshold"] = $"{expected.TransparencyThreshold}",
            ["BitDepth"] = $"{expected.BitDepth}",
            ["PngColorType"] = $"{expected.PngColorType}",
            ["CompressionLevel"] = $"{expected.CompressionLevel}",
            ["PngFilterMethod"] = $"{expected.PngFilterMethod}",
            ["Sampler"] = expected.Sampler,
            ["GifColorTableMode"] = $"{expected.GifColorTableMode}",
            ["MaxColors"] = $"{expected.MaxColors}",
            ["Dither"] = $"{expected.Dither}",
            ["DevicePixelRatio"] = $"{expected.DevicePixelRatio}"
        };
        return defaults;
    }
}