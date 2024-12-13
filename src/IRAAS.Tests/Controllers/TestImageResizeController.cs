using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IRAAS.Controllers;
using IRAAS.Exceptions;
using IRAAS.ImageProcessing;
using IRAAS.Security;
using IRAAS.Tests.Fakes;
using IRAAS.Tests.ImageProcessing;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using static PeanutButter.RandomGenerators.RandomValueGen;
using NExpect;
using NSubstitute;
using PeanutButter.DuckTyping.Extensions;
using PeanutButter.Utils;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using static NExpect.Expectations;

namespace IRAAS.Tests.Controllers;

[TestFixture]
public class TestImageResizeController
{
    [Test]
    public void Controller_ShouldHaveEmptyRoute()
    {
        // Arrange
        // Act
        Expect(typeof(ImageResizeController))
            .To.Have.Route("");
        // Assert
    }

    [TestFixture]
    public class Resize
    {
        [Test]
        public void ShouldHaveEmptyRoute()
        {
            // Arrange
            // Act
            Expect(typeof(ImageResizeController))
                .To.Have.Route(nameof(ImageResizeController.Resize), "");
            // Assert
        }

        [Test]
        public void ShouldThrowImageSourceNotAllowedWhenWhitelistSaysNotAllowed()
        {
            // Arrange
            var whiteList = Substitute.For<IWhitelist>();
            whiteList.IsAllowed(Arg.Any<string>()).Returns(false);
            var sut = Create(whiteList: whiteList);
            var options = new ImageResizeOptions()
            {
                Url = GetRandomHttpUrl()
            };
            // Act
            Expect(() => sut.Resize(options))
                .To.Throw<ImageSourceNotAllowedException>()
                .With.Property(e => e.Url)
                .Equal.To(options.Url);
            // Assert
        }

        [Test]
        public void ShouldPassOptionsToResizer()
        {
            // Arrange
            var resizer = Substitute.For<IImageResizer>();
            resizer.Resize(Arg.Any<ImageResizeOptions>(), Arg.Any<IDictionary<string, string>>())
                .Returns(new StreamAndHeaders(new MemoryStream(), new Dictionary<string, string>()));
            var sut = Create(resizer);
            var options = GetRandom<ImageResizeOptions>();
            // Act
            sut.Resize(options);
            // Assert
            Expect(resizer).To.Have.Received(1).Resize(
                options,
                Arg.Any<IDictionary<string, string>>()
            );
        }

        [Test]
        public void ShouldPassCurrentHeadersToResizer()
        {
            // Arrange
            var resizer = Substitute.For<IImageResizer>();
            resizer.Resize(
                Arg.Any<ImageResizeOptions>(),
                Arg.Any<IDictionary<string, string>>()
            ).Returns(new StreamAndHeaders(new MemoryStream(), new Dictionary<string, string>()));
            var headers = new Dictionary<string, string>()
            {
                [GetRandomString(10)] = GetRandomString(10)
            };
            var httpContext = new FakeHttpContext(headers);
            var accessor = Substitute.For<IHttpContextAccessor>();
            accessor.HttpContext.Returns(httpContext);
            var options = GetRandom<ImageResizeOptions>();
            var sut = Create(resizer, httpContextAccessor: accessor);
            // Act
            sut.Resize(options);
            // Assert
            Expect(resizer).To.Have.Received(1)
                .Resize(
                    options,
                    Arg.Is<IDictionary<string, string>>(
                        dict => dict.IsEquivalentTo(headers)
                    )
                );
        }

        [Test]
        public async Task ShouldReturnFileStreamResultWithStreamFromResizer()
        {
            // Arrange
            var resizer = Substitute.For<IImageResizer>();
            var expected = GetRandomBytes(1024);
            var processStream = new MemoryStream(expected);
            var options = GetRandom<ImageResizeOptions>();
            resizer.Resize(
                options,
                Arg.Any<IDictionary<string, string>>()
            ).Returns(
                new StreamAndHeaders(processStream, new Dictionary<string, string>())
            );
            var sut = Create(resizer);
            // Act
            var result = await sut.Resize(options);
            // Assert
            var resultStream = new MemoryStream();
            result.FileStream.CopyTo(resultStream);
            Expect(resultStream.ToArray()).To.Equal(expected);
        }

        [Test]
        public async Task ShouldSetMimeTypeFromMimeTypeProvider()
        {
            // Arrange
            // Content-Type header is checked by FileStreamResult, so
            //  must be valid(-ish)
            var expected = GetRandomFrom(
                new[]
                {
                    "image/jpeg",
                    "image/png",
                    "application/octet-stream"
                }
            );
            var resizer = Substitute.For<IImageResizer>();
            var options = GetRandom<ImageResizeOptions>();
            var imageStream = new MemoryStream();
            resizer.Resize(options, Arg.Any<IDictionary<string, string>>()).Returns(
                new StreamAndHeaders(imageStream, new Dictionary<string, string>())
            );
            var mimeTypeProvider = Substitute.For<IImageMimeTypeProvider>();
            mimeTypeProvider.DetermineMimeTypeFor(imageStream)
                .Returns(expected);
            var sut = Create(resizer, mimeTypeProvider);
            // Act
            var result = await sut.Resize(options);
            // Assert
            Expect(result.ContentType).To.Equal(expected);
        }

        [Test]
        public void ShouldSetResponseHeadersFromResizer()
        {
            // Arrange
            var key = GetRandomString(10);
            var value = GetRandomString(10);
            var headers = new Dictionary<string, string>()
            {
                [key] = value
            };
            var resizer = Substitute.For<IImageResizer>();
            var options = GetRandom<ImageResizeOptions>();
            var imageStream = new MemoryStream();
            resizer.Resize(
                    options,
                    Arg.Any<IDictionary<string, string>>()
                )
                .Returns(
                    new StreamAndHeaders(
                        imageStream,
                        headers
                    )
                );
            var httpContext = new FakeHttpContext();
            var accessor = Substitute.For<IHttpContextAccessor>();
            accessor.HttpContext.Returns(httpContext);
            var sut = Create(resizer, httpContextAccessor: accessor);
            // Act
            sut.Resize(options);
            // Assert
            var responseHeaders = accessor.HttpContext.Response.Headers.ToDictionary();
            Expect(responseHeaders[key]).To.Equal(value);
        }

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
            var expectedTransparencyThreshold = (byte)GetRandomInt(0, 255);
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

        [TearDown]
        public void Teardown()
        {
            ImageResizeOptions.SetDefaults(null);
        }

        private ImageResizeController Create(
            IImageResizer resizer = null,
            IImageMimeTypeProvider mimeTypeProvider = null,
            IWhitelist whiteList = null,
            IHttpContextAccessor httpContextAccessor = null
        )
        {
            return new ImageResizeController(
                resizer ?? Substitute.For<IImageResizer>(),
                mimeTypeProvider ?? CreateFakeMimeTypeProvider(),
                whiteList ?? CreateAllowingWhitelist(),
                httpContextAccessor ?? CreateFakeHttpContextAccessor()
            );
        }

        private IHttpContextAccessor CreateFakeHttpContextAccessor()
        {
            var result = Substitute.For<IHttpContextAccessor>();
            result.HttpContext.Returns(new FakeHttpContext());
            return result;
        }

        private IWhitelist CreateAllowingWhitelist()
        {
            var result = Substitute.For<IWhitelist>();
            result.IsAllowed(Arg.Any<string>())
                .Returns(true);
            return result;
        }

        private IImageMimeTypeProvider CreateFakeMimeTypeProvider()
        {
            var result = Substitute.For<IImageMimeTypeProvider>();
            result.DetermineMimeTypeFor(Arg.Any<Stream>())
                .Returns("image/jpeg");
            return result;
        }
    }
}