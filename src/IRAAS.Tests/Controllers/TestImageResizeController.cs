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
using NSubstitute;
using PeanutButter.TestUtils.AspNetCore.Builders;
using PeanutButter.Utils;

namespace IRAAS.Tests.Controllers;

[TestFixture]
public class TestImageResizeController : TestBase
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
    public class Resize : TestBase
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
        public async Task ShouldPassOptionsToResizer()
        {
            // Arrange
            var resizer = Substitute.For<IImageResizer>();
            resizer.Resize(Arg.Any<ImageResizeOptions>(), Arg.Any<IDictionary<string, string>>())
                .Returns(new StreamAndHeaders(new MemoryStream(), new Dictionary<string, string>()));
            var sut = Create(resizer);
            var options = GetRandom<ImageResizeOptions>();
            // Act
            await sut.Resize(options);
            // Assert
            await Expect(resizer).To.Have.Received(1)
                .Resize(
                    options,
                    Arg.Any<IDictionary<string, string>>()
                );
        }

        [Test]
        public async Task ShouldPassCurrentHeadersToResizer()
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
            await sut.Resize(options);
            // Assert
            await Expect(resizer)
                .To.Have.Received(1)
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
            await result.FileStream.CopyToAsync(resultStream);
            Expect(resultStream.ToArray()).To.Equal(expected);
        }

        [Test]
        public async Task ShouldSetMimeTypeFromMimeTypeProvider()
        {
            // Arrange
            // Content-Type header is checked by FileStreamResult, so
            //  must be valid(-ish)
            var expected = GetRandomFrom(
                [
                    "image/jpeg",
                    "image/png",
                    "application/octet-stream"
                ]
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
            Expect(result.ContentType)
                .To.Equal(expected);
        }

        [Test]
        public async Task ShouldSetResponseHeadersFromResizer()
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
            await sut.Resize(options);
            // Assert
            var responseHeaders = accessor.HttpContext!.Response.Headers.ToDictionary();
            Expect(responseHeaders[key])
                .To.Equal(value);
        }

        [TestFixture]
        public class FallingBackOnConfiguredDefaults
        {
            [Test]
            public async Task ShouldHaveDefaultsFillesAutomatically()
            {
                // Arrange
                var imageName = $"{GetRandomString()}.png";
                var imageData = GetRandomBytes();
                var expected = GetRandom<StreamAndHeaders>();
                var expectedMimeType = GetRandomFrom(
                    [
                        "image/png",
                        "image/jpg",
                        "image/bpm"
                    ]
                );
                using var lease = TestEnvironment.BorrowHttpServer();
                var server = lease.Instance;
                server.ServeFile($"/{imageName}", imageData, "image/png");
                var defaults = GetRandom<DefaultImageResizeParameters>();
                ImageResizeOptions.SetDefaults(defaults);
                var options = new ImageResizeOptions()
                {
                    Url = server.GetFullUrlFor($"/{imageName}")
                };
                var resizer = Substitute.For<IImageResizer>()
                    .With(
                        o => o.Resize(
                            Arg.Any<ImageResizeOptions>(),
                            Arg.Any<IDictionary<string, string>>()
                        ).Returns(_ => expected)
                    );
                var mimeTypeProvider = Substitute.For<IImageMimeTypeProvider>()
                    .With(
                        o => o.DetermineMimeTypeFor(expected.Stream)
                            .Returns(_ => expectedMimeType)
                    );
                var httpContext = HttpContextBuilder.BuildRandom();
                var accessor = Substitute.For<IHttpContextAccessor>()
                    .For(httpContext);
                var expectedOptions = new ImageResizeOptions();
                options.CopyPropertiesTo(expectedOptions);
                defaults.CopyPropertiesTo(expectedOptions);
                var expectedHeaders = httpContext.Response.Headers
                    .ToDictionary();
                foreach (var header in expected.Headers)
                {
                    expectedHeaders[header.Key] = header.Value;
                }

                var sut = Create(
                    resizer,
                    mimeTypeProvider,
                    httpContextAccessor: accessor
                );
                // Act
                var result = await sut.Resize(options);
                // Assert
                await Expect(resizer)
                    .To.Have.Received(1)
                    .Resize(
                        Arg.Is<ImageResizeOptions>(
                            o => o.DeepEquals(expectedOptions)
                        ),
                        Arg.Is<IDictionary<string, string>>(
                            o => o.IsEquivalentTo(httpContext.Request.Headers.ToDictionary())
                        )
                    );
                Expect(mimeTypeProvider)
                    .To.Have.Received(1)
                    .DetermineMimeTypeFor(result.FileStream);
                Expect(result.ContentType)
                    .To.Equal(expectedMimeType);
            }
        }

        private static ImageResizeController Create(
            IImageResizer resizer = null,
            IImageMimeTypeProvider mimeTypeProvider = null,
            IWhitelist whiteList = null,
            IHttpContextAccessor httpContextAccessor = null
        )
        {
            ImageResizeOptions.SetDefaults(null);
            return new ImageResizeController(
                resizer ?? Substitute.For<IImageResizer>(),
                mimeTypeProvider ?? CreateFakeMimeTypeProvider(),
                whiteList ?? CreateAllowingWhitelist(),
                httpContextAccessor ?? CreateFakeHttpContextAccessor()
            );
        }

        private static IHttpContextAccessor CreateFakeHttpContextAccessor()
        {
            var result = Substitute.For<IHttpContextAccessor>();
            result.HttpContext.Returns(new FakeHttpContext());
            return result;
        }

        private static IWhitelist CreateAllowingWhitelist()
        {
            var result = Substitute.For<IWhitelist>();
            result.IsAllowed(Arg.Any<string>())
                .Returns(true);
            return result;
        }

        private static IImageMimeTypeProvider CreateFakeMimeTypeProvider()
        {
            var result = Substitute.For<IImageMimeTypeProvider>();
            result.DetermineMimeTypeFor(Arg.Any<Stream>())
                .Returns("image/jpeg");
            return result;
        }
    }
}