using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using IRAAS.Controllers;
using IRAAS.ImageProcessing;
using IRAAS.Tests.Fakes;
using IRAAS.Tests.ImageProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public class ResizeByUrl : TestBase
    {
        [Test]
        public void ShouldHaveEmptyRouteForGetRequest()
        {
            // Arrange
            // Act
            Expect(typeof(ImageResizeController))
                .To.Have.Method(nameof(ImageResizeController.ResizeByUrl))
                .With.Route("")
                .Supporting(HttpMethod.Get);
            // Assert
        }

        [Test]
        public async Task ShouldPassOptionsToResizer()
        {
            // Arrange
            var resizer = Substitute.For<IImageResizer>();
            resizer.Resize(Arg.Any<ImageUrlResizeParameters>(), Arg.Any<IDictionary<string, string>>())
                .Returns(new StreamAndHeaders(new MemoryStream(), new Dictionary<string, string>()));
            var sut = Create(resizer);
            var options = GetRandom<ImageUrlResizeParameters>();
            // Act
            await sut.ResizeByUrl(options);
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
                Arg.Any<ImageUrlResizeParameters>(),
                Arg.Any<IDictionary<string, string>>()
            ).Returns(new StreamAndHeaders(new MemoryStream(), new Dictionary<string, string>()));
            var headerKey = GetRandomString(10);
            var headerValue = GetRandomString(10);
            var headers = new Dictionary<string, string>()
            {
                [headerKey] = headerValue
            };
            var httpContext = HttpContextBuilder.Create()
                .WithRequestHeader(headerKey, headerValue)
                .Build();
            var accessor = Substitute.For<IHttpContextAccessor>();
            accessor.HttpContext.Returns(httpContext);
            var options = GetRandom<ImageUrlResizeParameters>();
            var sut = Create(resizer, httpContextAccessor: accessor);
            // Act
            await sut.ResizeByUrl(options);
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
            var options = GetRandom<ImageUrlResizeParameters>();
            resizer.Resize(
                options,
                Arg.Any<IDictionary<string, string>>()
            ).Returns(
                new StreamAndHeaders(processStream, new Dictionary<string, string>())
            );
            var sut = Create(resizer);
            // Act
            var result = await sut.ResizeByUrl(options);
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
            var options = GetRandom<ImageUrlResizeParameters>();
            var imageStream = new MemoryStream();
            resizer.Resize(options, Arg.Any<IDictionary<string, string>>()).Returns(
                new StreamAndHeaders(imageStream, new Dictionary<string, string>())
            );
            var mimeTypeProvider = Substitute.For<IImageMimeTypeProvider>();
            mimeTypeProvider.DetermineMimeTypeFor(imageStream)
                .Returns(expected);
            var sut = Create(resizer, mimeTypeProvider);
            // Act
            var result = await sut.ResizeByUrl(options);
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
            var options = GetRandom<ImageUrlResizeParameters>();
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
            var httpContext = HttpContextBuilder.BuildDefault();
            var accessor = Substitute.For<IHttpContextAccessor>();
            accessor.HttpContext.Returns(httpContext);
            var sut = Create(resizer, httpContextAccessor: accessor);
            // Act
            await sut.ResizeByUrl(options);
            // Assert
            var responseHeaders = accessor.HttpContext!.Response.Headers.ToDictionary();
            Expect(responseHeaders[key])
                .To.Equal(value);
        }

        [TestFixture]
        public class FallingBackOnConfiguredDefaults
        {
            [Test]
            public async Task ShouldHaveDefaultsFilledAutomatically()
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
                server.ServeFile(
                    $"/{imageName}",
                    imageData,
                    "image/png"
                );
                var defaults = GetRandom<DefaultImageResizeParameters>();
                ImageResizeParameters.SetDefaults(defaults);
                var options = new ImageUrlResizeParameters()
                {
                    Url = server.GetFullUrlFor($"/{imageName}")
                };
                var resizer = Substitute.For<IImageResizer>()
                    .With(
                        o => o.Resize(
                            Arg.Any<ImageUrlResizeParameters>(),
                            Arg.Any<IDictionary<string, string>>()
                        ).Returns(_ => expected)
                    );
                var mimeTypeProvider = Substitute.For<IImageMimeTypeProvider>()
                    .With(
                        o => o.DetermineMimeTypeFor(expected.Stream)
                            .Returns(_ => expectedMimeType)
                    );
                var httpContext = HttpContextBuilder.BuildDefault();
                var accessor = Substitute.For<IHttpContextAccessor>()
                    .For(httpContext);
                var expectedOptions = new ImageUrlResizeParameters();
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
                var result = await sut.ResizeByUrl(options);
                // Assert
                await Expect(resizer)
                    .To.Have.Received(1)
                    .Resize(
                        options,
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
    }

    [TestFixture]
    public class ResizeByImageData
    {
        [Test]
        public void ShouldHaveEmptyRouteForPostRequest()
        {
            // Arrange
            // Act
            Expect(typeof(ImageResizeController))
                .To.Have.Method(nameof(ImageResizeController.ResizeByImageData))
                .With.Route("")
                .Supporting(HttpMethod.Post);
            // Assert
        }

        [Test]
        public async Task ShouldResizeImageAndReturnResultWithDeterminedMimeType()
        {
            // Arrange
            var parameters = GetRandom<ImageDataResizeParameters>();
            var stream = new MemoryStream(GetRandomBytes());
            var expectedMimeType = GetRandomMimeType();
            var streamAndHeaders = new StreamAndHeaders(
                stream,
                GetRandom<Dictionary<string, string>>()
            );
            Expect(stream.CanRead)
                .To.Be.True();
            Expect(stream.CanWrite)
                .To.Be.True();
            var imageResizer = Substitute.For<IImageResizer>()
                .With(
                    o => o.Resize(Arg.Any<ImageDataResizeParameters>())
                        .Returns(_ => Task.FromResult(streamAndHeaders))
                );
            var mimeTypeProvider = Substitute.For<IImageMimeTypeProvider>()
                .With(
                    o => o.DetermineMimeTypeFor(Arg.Any<Stream>())
                        .Returns(_ => expectedMimeType)
                );
            var sut = Create(imageResizer, mimeTypeProvider);

            // Act
            var result = await sut.ResizeByImageData(parameters);

            // Assert
            Expect(result)
                .To.Be.An.Instance.Of<FileStreamResult>();
            Expect(result.FileStream)
                .To.Be(stream);
            Expect(result.ContentType)
                .To.Equal(expectedMimeType);

            // verify that the stream was not disposed
            // -> the returned FileStreamResult will dispose
            // of the internal stream
            Expect(stream.CanRead)
                .To.Be.True();
            Expect(stream.CanWrite)
                .To.Be.True();
        }

        [Test]
        public void ShouldThrowArgumentExceptionWhenInputDataExceedsConfiguredMaxInputImageSize()
        {
            // Arrange
            var parameters = new ImageDataResizeParameters()
            {
                ImageData = GetRandomBytes(1000, 2000)
            };
            var appSettings = Substitute.For<IAppSettings>()
                .With(o => o.AllowPostRequests.Returns(true))
                .With(o => o.PostAuthTokens.Returns("*"))
                .With(o => o.MaxInputImageSize.Returns(_ => parameters.ImageData.Length - 1));
            var resizer = Substitute.For<IImageResizer>();
            var sut = Create(resizer, appSettings: appSettings);

            // Act
            Expect(async () => await sut.ResizeByImageData(parameters))
                .To.Throw<ArgumentException>()
                .For(nameof(parameters.ImageData));

            // Assert
        }
    }

    private static ImageResizeController Create(
        IImageResizer resizer = null,
        IImageMimeTypeProvider mimeTypeProvider = null,
        IHttpContextAccessor httpContextAccessor = null,
        IAppSettings appSettings = null
    )
    {
        ImageResizeParameters.SetDefaults(null);
        return new ImageResizeController(
            resizer ?? Substitute.For<IImageResizer>(),
            mimeTypeProvider ?? CreateFakeMimeTypeProvider(),
            httpContextAccessor ?? CreateFakeHttpContextAccessor(),
            appSettings ?? Substitute.For<IAppSettings>()
                .WithDefaultSettings()
        );
    }

    private static IHttpContextAccessor CreateFakeHttpContextAccessor()
    {
        var result = Substitute.For<IHttpContextAccessor>();
        result.HttpContext.Returns(HttpContextBuilder.BuildDefault());
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