using System;
using IRAAS.ImageProcessing;
using NExpect;
using NUnit.Framework;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using static NExpect.Expectations;
using static PeanutButter.RandomGenerators.RandomValueGen;

namespace IRAAS.Tests.ImageProcessing
{
    [TestFixture]
    public class TestImageResizeOptions
    {
        [Test]
        public void ShouldBeAbleToConstructWithNoParameters()
        {
            // since this is a model which asp.net will instantiate and
            //    attempt to populate
            // Arrange
            // Act
            Expect(() => Activator.CreateInstance(typeof(ImageResizeOptions)))
                .Not.To.Throw();
            // Assert
        }

        [TestFixture]
        public class Format
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
                Expect(sut.Format).To.Equal(JpegFormat.Instance.Name);
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
                Expect(sut.Format).To.Equal(GifFormat.Instance.Name);
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
                Expect(sut.Format).To.Equal(PngFormat.Instance.Name);
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
                Expect(sut.Format).To.Equal(BmpFormat.Instance.Name);
            }
        }

        [TestFixture]
        public class Quality
        {
            [Test]
            public void ShouldDefaultTo85()
            {
                // Arrange
                var sut = Create();
                // Act
                var result = sut.Quality;
                // Assert
                Expect(result).To.Equal(85);
                Expect(result).To.Equal(ImageResizeOptions.DEFAULT_QUALITY);
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
                Expect(sut.Quality).To.Equal(expected);
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
                Expect(sut.Quality).To.Equal(85);
            }
        }

        [TestFixture]
        public class Url
        {
            [Test]
            public void ShouldBeAbleToSetAndGetValidUriWithPath()
            {
                // Arrange
                var expected = GetRandomHttpUrlWithPath();
                var uri = new Uri(expected);
                Expect(uri.AbsolutePath).Not.To.Be.Null.Or.Whitespace();
                var sut = Create();
                // Act
                sut.Url = expected;
                // Assert
                Expect(sut.Url).To.Equal(expected);
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
                Expect(sut.Url).To.Be.Null();
            }

            [Test]
            public void ShouldNotBeAbleToSetToUriWithoutPath()
            {
                // Arrange
                var url = $"http://{GetRandomHostname()}";
                var uri = new Uri(url);
                Expect(uri.AbsolutePath).To.Equal("/");
                var sut = Create();
                // Act
                sut.Url = url;
                // Assert
                Expect(sut.Url).To.Be.Null();
            }
        }

        [TestFixture]
        public class Subsampling
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
                Expect(sut.JpegEncodingColor).To.Be.Null();
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
                Expect(sut.JpegEncodingColor).To.Equal(expected);
            }
        }

        [TestFixture]
        public class Dimensions
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
                Expect(sut.Width).To.Equal(expected);
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
                Expect(sut.Width).To.Be.Null();
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
                Expect(sut.Height).To.Equal(expected);
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
                Expect(sut.Height).To.Be.Null();
            }
        }

        [TestFixture]
        // ReSharper disable once InconsistentNaming
        public class DevicePixelRatio
        {
            [Test]
            public void ShouldDefaultTo1()
            {
                // Arrange
                var sut = Create();
                // Act
                var result = sut.DevicePixelRatio;
                // Assert
                Expect(result).To.Equal(1);
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
                Expect(result).To.Equal(1);
            }
        }


        private static ImageResizeOptions Create()
        {
            return new ImageResizeOptions();
        }
    }
}