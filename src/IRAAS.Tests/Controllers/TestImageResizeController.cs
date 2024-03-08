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
using static NExpect.Expectations;

namespace IRAAS.Tests.Controllers
{
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
                    options, Arg.Any<IDictionary<string, string>>());
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
                    .Resize(options,
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
                    Arg.Any<IDictionary<string, string>>()).Returns(
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
                var expected = GetRandomFrom(new[]
                {
                    "image/jpeg",
                    "image/png",
                    "application/octet-stream"
                });
                var resizer = Substitute.For<IImageResizer>();
                var options = GetRandom<ImageResizeOptions>();
                var imageStream = new MemoryStream();
                resizer.Resize(options, Arg.Any<IDictionary<string, string>>()).Returns(
                    new StreamAndHeaders(imageStream, new Dictionary<string, string>()));
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
                    Arg.Any<IDictionary<string, string>>())
                    .Returns(new StreamAndHeaders(
                        imageStream, headers));
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

            private ImageResizeController Create(
                IImageResizer resizer = null,
                IImageMimeTypeProvider mimeTypeProvider = null,
                IWhitelist whiteList = null,
                IHttpContextAccessor httpContextAccessor = null)
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
}