using System;
using System.Collections.Generic;
using System.Linq;
using IRAAS.ImageProcessing;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.Utils;
using PeanutButter.Utils.Dictionaries;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace IRAAS.Tests.ImageProcessing;

[TestFixture]
public class TestImageResizeParameters : TestBase
{
    [Test]
    public void ShouldBeAbleToConstructWithNoParameters()
    {
        // since this is a model which asp.net will instantiate and
        //    attempt to populate
        // Arrange
        // Act
        Expect(() => Activator.CreateInstance(typeof(ImageResizeParameters)))
            .Not.To.Throw();
        // Assert
    }

    [TestFixture]
    public class Format : TestBase
    {
        [TestCase("jpg")]
        [TestCase("jpeg")]
        [TestCase("Jpg")]
        [TestCase("JPEG")]
        [TestCase("wibbly-wobbly")]
        public void ShouldResolve_JPEG_for(string format)
        {
            // Arrange
            var sut = Create();
            // Act
            sut.Format = format;
            // Assert
            Expect(sut.Format)
                .To.Equal(JpegFormat.Instance.Name);
        }

        [TestCase("gif")]
        [TestCase("GIF")]
        public void ShouldResolve_GIF_for(string format)
        {
            // Arrange
            var sut = Create();
            // Act
            sut.Format = format;
            // Assert
            Expect(sut.Format)
                .To.Equal(GifFormat.Instance.Name);
        }

        [TestCase("png")]
        [TestCase("PnG")]
        public void ShouldResolve_PNG_For(string format)
        {
            // Arrange
            var sut = Create();
            // Act
            sut.Format = format;
            // Assert
            Expect(sut.Format)
                .To.Equal(PngFormat.Instance.Name);
        }

        [TestCase("BMP")]
        [TestCase("bmP")]
        public void ShouldResolve_BMP_For(string format)
        {
            // Arrange
            var sut = Create();
            // Act
            sut.Format = format;
            // Assert
            Expect(sut.Format)
                .To.Equal(BmpFormat.Instance.Name);
        }
    }

    [TestFixture]
    public class Quality : TestBase
    {
        [Test]
        public void ShouldDefaultTo85()
        {
            // Arrange
            var sut = Create();
            // Act
            var result = sut.Quality;
            // Assert
            Expect(result)
                .To.Equal(85);
            Expect(result)
                .To.Equal(ImageResizeParameters.DEFAULT_QUALITY);
        }

        [Test]
        public void ShouldBeAbleToSetQualityToValueBetween0And100()
        {
            // Arrange
            var expected = GetRandomInt(1, 100);
            var sut = Create();
            // Act
            sut.Quality = expected;
            // Assert
            Expect(sut.Quality)
                .To.Equal(expected);
        }

        [TestCase(101)]
        [TestCase(-12)]
        public void ShouldResetTo85WhenGivenBadValue(int invalid)
        {
            // Arrange
            var valid = GetRandomInt(1, 100);
            var sut = Create();
            // Act
            sut.Quality = valid;
            sut.Quality = invalid;
            // Assert
            Expect(sut.Quality)
                .To.Equal(85);
        }
    }

    [TestFixture]
    public class Url : TestBase
    {
        [Test]
        public void ShouldBeAbleToSetAndGetValidUriWithPath()
        {
            // Arrange
            var expected = GetRandomHttpUrlWithPath();
            var uri = new Uri(expected);
            Expect(uri.AbsolutePath)
                .Not.To.Be.Null.Or.Whitespace();
            var sut = Create();
            // Act
            sut.Url = expected;
            // Assert
            Expect(sut.Url)
                .To.Equal(expected);
        }

        [Test]
        public void ShouldNotBeAbleToSetInvalidUri()
        {
            // Arrange
            var valid = GetRandomHttpUrlWithPath();
            var invalid = GetRandomString(1, 100);
            var sut = Create();
            // Act
            sut.Url = valid;
            sut.Url = invalid;
            // Assert
            Expect(sut.Url)
                .To.Be.Null();
        }

        [Test]
        public void ShouldNotBeAbleToSetToUriWithoutPath()
        {
            // Arrange
            var url = $"http://{GetRandomHostname()}";
            var uri = new Uri(url);
            Expect(uri.AbsolutePath)
                .To.Equal("/");
            var sut = Create();
            // Act
            sut.Url = url;
            // Assert
            Expect(sut.Url)
                .To.Be.Null();
        }
    }

    [TestFixture]
    public class Subsampling : TestBase
    {
        [Test]
        public void ShouldDefaultNull()
        {
            // when not specified, ImageSharp selects a
            // sub-sampler based on the quality value
            // -> JpegEncoderCore.cs:193
            // Arrange
            var sut = Create();
            // Act
            Expect(sut.JpegEncodingColor)
                .To.Be.Null();
            // Assert
        }

        [Test]
        public void ShouldBeSettable()
        {
            // Arrange
            var expected = GetRandom<JpegEncodingColor>();
            var sut = Create();
            // Act
            sut.JpegEncodingColor = expected;
            // Assert
            Expect(sut.JpegEncodingColor)
                .To.Equal(expected);
        }
    }

    [TestFixture]
    public class Dimensions : TestBase
    {
        [Test]
        public void ShouldBeAbleToSetWidthGreaterThanZero()
        {
            // Arrange
            var expected = GetRandomInt(1, 10000);
            var sut = Create();
            // Act
            sut.Width = expected;
            // Assert
            Expect(sut.Width)
                .To.Equal(expected);
        }

        // allows caller to explicitly supply no width, by providing
        // 0 or less
        [Test]
        public void ShouldResetWidthToNullWithValueLessThan1()
        {
            // Arrange
            var value = GetRandomInt(-100, 0);
            var sut = Create();
            // Act
            sut.Width = value;
            // Assert
            Expect(sut.Width)
                .To.Be.Null();
        }

        [Test]
        public void ShouldBeAbleToSetHeightGreaterThanZero()
        {
            // Arrange
            var expected = GetRandomInt(1, 10000);
            var sut = Create();
            // Act
            sut.Height = expected;
            // Assert
            Expect(sut.Height)
                .To.Equal(expected);
        }

        // allows caller to explicitly supply no height, by providing
        // 0 or less
        [Test]
        public void ShouldResetHeightToNullWithValueLessThan1()
        {
            // Arrange
            var value = GetRandomInt(-100, 0);
            var sut = Create();
            // Act
            sut.Height = value;
            // Assert
            Expect(sut.Height)
                .To.Be.Null();
        }
    }

    [TestFixture]
    // ReSharper disable once InconsistentNaming
    public class DevicePixelRatio : TestBase
    {
        [Test]
        public void ShouldDefaultTo1()
        {
            // Arrange
            var sut = Create();
            // Act
            var result = sut.DevicePixelRatio;
            // Assert
            Expect(result)
                .To.Equal(1);
        }

        [Test]
        public void ShouldDefaultTo1IfProvidedValueLessThan1()
        {
            // Arrange
            var sut = Create();
            // Act
            sut.DevicePixelRatio = GetRandomDecimal(-1, 0.99M);
            var result = sut.DevicePixelRatio;
            // Assert
            Expect(result)
                .To.Equal(1);
        }
    }

    [TestFixture]
    public class ApplyDefaultsFor
    {
        [TestFixture]
        public class WhenSamplerNotSet
        {
            [TestFixture]
            public class AndHaveNoPerFormatDefaults
            {
                [Test]
                public void ShouldSetDefaultSampler()
                {
                    // Arrange
                    var defaults = GetRandom<IDefaultImageResizeParameters>()
                        .With(o => o.Sampler = GetRandomString());
                    using var _ = AutoResetter.Create(
                        () => ImageResizeParameters.SetDefaults(defaults),
                        ImageResizeParameters.ClearDefaults
                    );
                    var options = GetRandom<ImageResizeParameters>()
                        .With(o => o.Sampler = null);
                    // Act
                    options.ApplyDefaultsFor(GetRandomFrom(["jpg", "png", "bmp", "gif"]));
                    // Assert
                    Expect(options.Sampler)
                        .To.Equal(defaults.Sampler);
                }
            }

            [TestFixture]
            public class AndHavePerFormatDefaults
            {
                [Test]
                public void ShouldSetDefaultForFormat()
                {
                    // Arrange
                    var defaults = GetRandom<IDefaultImageResizeParameters>()
                        .With(o => o.Sampler = "default sampler");
                    var formatDefaults = GetRandom<IDefaultImageResizeParameters>()
                        .With(o => o.Sampler = "format sampler");
                    var inputFormat = GetRandomFrom(["jpg", "png", "bmp", "gif"]);
                    var formatDefaultProps = new DictionaryWrappingObject(formatDefaults)
                        .ToArray()
                        .ToDictionary(kvp => kvp.Key, kvp => $"{kvp.Value}");
                    defaults.RegisterPerFormatDefaultsFor(
                        inputFormat,
                        formatDefaultProps
                    );
                    using var _ = AutoResetter.Create(
                        () => ImageResizeParameters.SetDefaults(defaults),
                        ImageResizeParameters.ClearDefaults
                    );
                    var options = GetRandom<ImageResizeParameters>()
                        .With(o => o.Sampler = null);
                    Expect(options.Sampler)
                        .To.Be.Null();
                    // Act
                    options.ApplyDefaultsFor(inputFormat);
                    // Assert
                    Expect(options.Sampler)
                        .Not.To.Equal(defaults.Sampler);
                    Expect(options.Sampler)
                        .To.Equal(formatDefaults.Sampler);
                }
            }
        }
    }

    private static ImageResizeParameters Create()
    {
        return new ImageResizeParameters();
    }
}