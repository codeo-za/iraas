using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IRAAS.ImageProcessing;
using IRAAS.Tests.TestUtils;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.SimpleHTTPServer;
using PeanutButter.Utils;

// ReSharper disable RedundantAssignment
// ReSharper disable AccessToDisposedClosure

namespace IRAAS.Tests.ImageProcessing;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class TestUrlFetcher
{
    [Test]
    public void ShouldImplementIImageFetcher()
    {
        // Arrange
        // Act
        Expect(typeof(UrlFetcher))
            .To.Implement<IUrlFetcher>();
        // Assert
    }

    [Test]
    public async Task ShouldBeAbleToFetchExistingImage()
    {
        // Arrange
        using var server = HttpServerPool.Borrow();
        server.ServeFile(
            "/cat.jpg",
            () => Resources.Data.FluffyCatJpeg
        );
        var url = server.GetFullUrlFor("/cat.jpg");
        var sut = Create();
        // Act
        using var result = await sut.Fetch(url, new Dictionary<string, string>());
        // Assert
        var buffer = new byte[1024];
        using var memStream = new MemoryStream();
        var readCount = 0;
        do
        {
            readCount = result.Stream.Read(buffer, 0, 1024);
            memStream.Write(buffer, 0, readCount);
        } while (readCount > 0);

        var resultBytes = memStream.ToArray();
        Expect(resultBytes.Length)
            .To.Equal(Resources.Data.FluffyCatJpeg.Length);
        Expect(resultBytes)
            .To.Equal(Resources.Data.FluffyCatJpeg);
    }

    [Test]
    public async Task ShouldPassThroughRequestHeaders()
    {
        // Arrange
        var expectedHeader = GetRandomString();
        var expectedHeaderValue = GetRandomString();
        var captured = new Dictionary<string, string>();
        var source = new Dictionary<string, string>
        {
            [expectedHeader] = expectedHeaderValue
        };
        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (processor, _) =>
            {
                if (processor.Path != "/cat.bmp")
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                processor.HttpHeaders.ForEach(kvp => captured[kvp.Key] = kvp.Value);

                processor.WriteOKStatusHeader();
                processor.WriteMIMETypeHeader("image/bmp");
                processor.WriteConnectionClosesAfterCommsHeader();
                processor.WriteContentLengthHeader(
                    Resources.Data.FluffyCatBmp.Length
                );
                processor.WriteEmptyLineToStream();
                processor.WriteDataToStream(
                    Resources.Data.FluffyCatBmp
                );
                return HttpServerPipelineResult.Handled;
            }
        );
        var url = server.GetFullUrlFor("/cat.bmp");
        var sut = Create();
        // Act
        await sut.Fetch(url, source);
        // Assert
        Expect(captured)
            .To.Contain.Key(expectedHeader)
            .With.Value(expectedHeaderValue);
    }

    [Test]
    public async Task ShouldOverwriteHostHeaderToBeHostOfImage()
    {
        // Arrange
        var expectedHeader = "Host";
        var expectedHeaderValue = "localhost";
        var captured = new Dictionary<string, string>();
        var source = new Dictionary<string, string>
        {
            [expectedHeader] = GetRandomHostname()
        };
        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (processor, _) =>
            {
                if (processor.Path != "/cat.bmp")
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                processor.HttpHeaders.ForEach(kvp => captured[kvp.Key] = kvp.Value);

                processor.WriteOKStatusHeader();
                processor.WriteMIMETypeHeader("image/bmp");
                processor.WriteConnectionClosesAfterCommsHeader();
                processor.WriteContentLengthHeader(
                    Resources.Data.FluffyCatBmp.Length
                );
                processor.WriteEmptyLineToStream();
                processor.WriteDataToStream(
                    Resources.Data.FluffyCatBmp
                );
                return HttpServerPipelineResult.Handled;
            }
        );
        var url = server.GetFullUrlFor("/cat.bmp");
        var sut = Create();
        // Act
        await sut.Fetch(url, source);
        // Assert
        Expect(captured)
            .To.Contain.Key(expectedHeader)
            .With.Value(expectedHeaderValue);
    }

    [Test]
    public async Task ShouldOverwriteOriginHeaderToBeHostOfImage()
    {
        // Arrange
        var expectedHeader = "Origin";
        var expectedHeaderValue = "localhost";
        var captured = new Dictionary<string, string>();
        var source = new Dictionary<string, string>
        {
            [expectedHeader] = GetRandomHostname()
        };
        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (processor, _) =>
            {
                if (processor.Path.TrimStart('/') != "cat.bmp")
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                processor.HttpHeaders.ForEach(kvp => captured[kvp.Key] = kvp.Value);

                processor.WriteOKStatusHeader();
                processor.WriteMIMETypeHeader("image/bmp");
                processor.WriteConnectionClosesAfterCommsHeader();
                processor.WriteContentLengthHeader(
                    Resources.Data.FluffyCatBmp.Length
                );
                processor.WriteEmptyLineToStream();
                processor.WriteDataToStream(
                    Resources.Data.FluffyCatBmp
                );
                return HttpServerPipelineResult.Handled;
            }
        );
        var url = server.GetFullUrlFor("/cat.bmp");
        var sut = Create();
        // Act
        await sut.Fetch(url, source);
        // Assert
        Expect(captured)
            .To.Contain.Key(expectedHeader)
            .With.Value(expectedHeaderValue);
    }

    [Test]
    public async Task ShouldRemoveConnectionHeader()
    {
        // because IRAAS will _never_ want to keep the connection alive
        // Arrange
        var expectedHeader = "Connection";
        var captured = new Dictionary<string, string>();
        var source = new Dictionary<string, string>
        {
            [expectedHeader] = "keep-alive"
        };
        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (processor, _) =>
            {
                if (processor.Path != "/cat.bmp")
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                processor.HttpHeaders.ForEach(kvp => captured[kvp.Key] = kvp.Value);

                processor.WriteOKStatusHeader();
                processor.WriteMIMETypeHeader("image/bmp");
                processor.WriteConnectionClosesAfterCommsHeader();
                processor.WriteContentLengthHeader(
                    Resources.Data.FluffyCatBmp.Length
                );
                processor.WriteEmptyLineToStream();
                processor.WriteDataToStream(
                    Resources.Data.FluffyCatBmp
                );
                return HttpServerPipelineResult.Handled;
            }
        );
        var url = server.GetFullUrlFor("/cat.bmp");
        var sut = Create();
        // Act
        await sut.Fetch(url, source);
        // Assert
        Expect(captured)
            .To.Contain.Key(expectedHeader)
            .With.Value("close");
    }

    [TestCase("image/*")]
    public async Task ShouldOverwriteContentHeaderToBe_(string expected)
    {
        // Arrange
        var expectedHeader = "Accept";
        var expectedHeaderValue = "image/*";
        var captured = new Dictionary<string, string>();
        var source = new Dictionary<string, string>
        {
            [expectedHeader] = "application/octet-stream"
        };
        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (processor, _) =>
            {
                if (processor.Path != "/cat.bmp")
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                processor.HttpHeaders.ForEach(kvp => captured[kvp.Key] = kvp.Value);

                processor.WriteOKStatusHeader();
                processor.WriteMIMETypeHeader("image/bmp");
                processor.WriteConnectionClosesAfterCommsHeader();
                processor.WriteContentLengthHeader(
                    Resources.Data.FluffyCatBmp.Length
                );
                processor.WriteEmptyLineToStream();
                processor.WriteDataToStream(
                    Resources.Data.FluffyCatBmp
                );
                return HttpServerPipelineResult.Handled;
            }
        );
        var url = server.GetFullUrlFor("/cat.bmp");
        var sut = Create();
        // Act
        await sut.Fetch(url, source);
        // Assert
        Expect(captured)
            .To.Contain.Key(expectedHeader)
            .With.Value(expectedHeaderValue);
    }

    [Test]
    public async Task ShouldReturnHeadersFromRemoteRequest()
    {
        // Arrange
        var expectedHeader = GetRandomString();
        var expectedHeaderValue = GetRandomString();
        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (processor, _) =>
            {
                if (processor.Path != "/cat.bmp")
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                processor.WriteOKStatusHeader();
                processor.WriteMIMETypeHeader("image/bmp");
                processor.WriteConnectionClosesAfterCommsHeader();
                processor.WriteHeader(expectedHeader, expectedHeaderValue);
                processor.WriteContentLengthHeader(
                    Resources.Data.FluffyCatBmp.Length
                );
                processor.WriteEmptyLineToStream();
                processor.WriteDataToStream(
                    Resources.Data.FluffyCatBmp
                );
                return HttpServerPipelineResult.Handled;
            }
        );
        var url = server.GetFullUrlFor("/cat.bmp");
        var sut = Create();
        // Act
        var result = await sut.Fetch(url, new Dictionary<string, string>());
        // Assert
        Expect(result.Headers)
            .Not.To.Be.Null();
        Expect(result.Headers)
            .Not.To.Be.Empty();
        Expect(result.Headers)
            .To.Contain.Key(expectedHeader)
            .With.Value(expectedHeaderValue);
    }

    [MaxTime(15000)]
    [TestCase(HttpStatusCode.MovedPermanently)]
    [Parallelizable(ParallelScope.None)] // timed; when run in parallel, flakes out
    public async Task ShouldDownloadFromLocationHeaderOn_(HttpStatusCode code)
    {
        // Arrange
        var originalCatRequested = false;
        var newCatRequested = false;

        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (processor, _) =>
            {
                if (processor.Path != "/cat.bmp")
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                originalCatRequested = true;

                // redirect
                processor.WriteStatusHeader(code, "Moved Permanently");
                processor.WriteHeader("Location", server.GetFullUrlFor("/new-cat.bmp"));
                processor.WriteEmptyLineToStream();
                processor.WriteContentLengthHeader(0);
                return HttpServerPipelineResult.HandledExclusively;
            }
        );

        server.AddHandler(
            (processor, _) =>
            {
                if (processor.Path != "/new-cat.bmp")
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                newCatRequested = true;
                processor.WriteOKStatusHeader();
                processor.WriteMIMETypeHeader("image/bmp");
                processor.WriteConnectionClosesAfterCommsHeader();
                processor.WriteContentLengthHeader(
                    Resources.Data.FluffyCatBmp.Length
                );
                processor.WriteEmptyLineToStream();
                processor.WriteDataToStream(
                    Resources.Data.FluffyCatBmp
                );
                return HttpServerPipelineResult.Handled;
            }
        );

        var sut = Create();
        var url = server.GetFullUrlFor("/cat.bmp");
        // Act
        using var result = await sut.Fetch(url, new Dictionary<string, string>());
        // Assert
        Expect(originalCatRequested)
            .To.Be.True("original cat was not requested");
        Expect(newCatRequested)
            .To.Be.True("new-cat was not requested");
        var buffer = new byte[1024];
        await using var memStream = new MemoryStream();
        var readCount = 0;
        do
        {
            readCount = result.Stream.Read(buffer, 0, 1024);
            memStream.Write(buffer, 0, readCount);
        } while (readCount > 0);

        var resultBytes = memStream.ToArray();
        Expect(resultBytes.Length)
            .To.Equal(Resources.Data.FluffyCatBmp.Length);
        Expect(resultBytes)
            .To.Equal(Resources.Data.FluffyCatBmp);
    }

    [MaxTime(18000)]
    [TestCase(HttpStatusCode.MovedPermanently)]
    [Parallelizable(ParallelScope.None)] // timed; when run in parallel, flakes out
    public void ShouldReturn500WhenNoLocationHeaderFor_(HttpStatusCode code)
    {
        // Arrange
        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (processor, _) =>
            {
                if (processor.Path != "/cat.bmp")
                {
                    return HttpServerPipelineResult.NotHandled;
                }

                // redirect
                processor.WriteStatusHeader(code, "Moved Permanently");
                processor.WriteEmptyLineToStream();
                processor.WriteContentLengthHeader(0);
                return HttpServerPipelineResult.HandledExclusively;
            }
        );

        var sut = Create();
        var url = server.GetFullUrlFor("/cat.bmp");
        // Act
        Expect(async () => await sut.Fetch(url, new Dictionary<string, string>()))
            .To.Throw<ImageProviderErrorException>()
            .With.Property(e => e.StatusCode)
            .Equal.To(HttpStatusCode.InternalServerError);
        // Assert
    }

    [Test]
    public void ShouldThrowImageProviderErrorExceptionWhenWebRequestB0Rks()
    {
        // Arrange
        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (processor, _) =>
            {
                processor.WriteFailure(HttpStatusCode.InternalServerError, "Internal server error");
                return HttpServerPipelineResult.HandledExclusively;
            }
        );
        var sut = Create();
        var url = server.GetFullUrlFor("cat.jpg");
        // Act
        Expect(async () => await sut.Fetch(url, new Dictionary<string, string>()))
            .To.Throw<ImageProviderErrorException>();
        // Assert
    }

    [Test]
    public void ShouldTimeOutRequestsAsPerConfig()
    {
        // Arrange

        var config = CreateDefaultAppSettings();
        config.MaxImageFetchTimeInMilliseconds.Returns(1000);
        using var server = HttpServerPool.Borrow();
        server.AddHandler(
            (_, _) =>
            {
                Thread.Sleep(2000);
                return HttpServerPipelineResult.NotHandled;
            }
        );
        var url = server.GetFullUrlFor(GetRandomString());
        var sut = Create(config);
        // Act
        Expect(async () => await sut.Fetch(url, new Dictionary<string, string>()))
            .To.Throw<RequestTimedOutException>()
            .With.Property(e => e.Url)
            .Equal.To(url);
        // Assert
    }

    [TestFixture]
    public class KeepAlive
    {
        [TestFixture]
        public class WhenDisabledInConfig
        {
            [TestFixture]
            public class AndNotSetInProvidedHeaders
            {
                [Test]
                public async Task ShouldDisableKeepAlive()
                {
                    // Arrange
                    var appSettings = CreateDefaultAppSettings();
                    appSettings.EnableConnectionKeepAlive.Returns(false);
                    var path = $"/{GetRandomString(2)}.jpg";
                    var connectionHeaderValue = null as string;
                    using var server = HttpServerPool.Borrow();
                    server.Serve(
                        path,
                        Resources.Data.FluffyCatJpeg,
                        "image/jpeg",
                        (p, _) =>
                        {
                            connectionHeaderValue = p.HttpHeaders["Connection"];
                        }
                    );
                    var sut = Create(appSettings);
                    // Act
                    await sut.Fetch(
                        server.GetFullUrlFor(path),
                        new Dictionary<string, string>()
                    );
                    // Assert
                    Expect(connectionHeaderValue.ToLower())
                        .To.Equal("close");
                }
            }

            [TestFixture]
            public class AndSetInHeaders : WhenDisabledInConfig
            {
                [Test]
                public async Task ShouldDisableKeepAlive()
                {
                    // Arrange
                    var appSettings = CreateDefaultAppSettings();
                    appSettings.EnableConnectionKeepAlive.Returns(false);
                    using var server = HttpServerPool.Borrow();
                    var path = $"/{GetRandomString(2)}.jpg";
                    var connectionHeaderValue = null as string;
                    server.Serve(
                        path,
                        Resources.Data.FluffyCatJpeg,
                        "image/jpeg",
                        (p, _) =>
                        {
                            connectionHeaderValue = p.HttpHeaders["Connection"];
                        }
                    );
                    var sut = Create(appSettings);
                    // Act
                    await sut.Fetch(
                        server.GetFullUrlFor(path),
                        new Dictionary<string, string>()
                        {
                            ["Connection"] = "keep-alive"
                        }
                    );
                    // Assert
                    Expect(connectionHeaderValue.ToLower())
                        .To.Equal("close");
                }
            }
        }

        [TestFixture]
        public class WhenEnabledInConfig
        {
            [TestFixture]
            public class AndNotSetInProvidedHeaders
            {
                [Test]
                public async Task ShouldDisableKeepAlive()
                {
                    // Arrange
                    var appSettings = CreateDefaultAppSettings();
                    appSettings.EnableConnectionKeepAlive.Returns(true);
                    var path = $"/{GetRandomString(2)}.jpg";
                    var connectionHeaderValue = null as string;
                    using var server = HttpServerPool.Borrow();
                    server.Serve(
                        path,
                        Resources.Data.FluffyCatJpeg,
                        "image/jpeg",
                        (p, _) =>
                        {
                            connectionHeaderValue = p.HttpHeaders["Connection"];
                        }
                    );
                    var sut = Create(appSettings);
                    // Act
                    await sut.Fetch(
                        server.GetFullUrlFor(path),
                        new Dictionary<string, string>()
                    );
                    // Assert
                    Expect(connectionHeaderValue.ToLower())
                        .To.Equal("keep-alive");
                }
            }

            [TestFixture]
            public class AndSetInHeaders
            {
                [Test]
                public async Task ShouldDisableKeepAlive()
                {
                    // Arrange
                    var appSettings = CreateDefaultAppSettings();
                    appSettings.EnableConnectionKeepAlive.Returns(true);
                    var path = $"/{GetRandomString(2)}.jpg";
                    var connectionHeaderValue = null as string;
                    using var server = HttpServerPool.Borrow();
                    server.Serve(
                        path,
                        Resources.Data.FluffyCatJpeg,
                        "image/jpeg",
                        (p, _) =>
                        {
                            connectionHeaderValue = p.HttpHeaders["Connection"];
                        }
                    );
                    var sut = Create(appSettings);
                    // Act
                    await sut.Fetch(
                        server.GetFullUrlFor(path),
                        new Dictionary<string, string>()
                        {
                            ["Connection"] = "close"
                        }
                    );
                    // Assert
                    Expect(connectionHeaderValue.ToLower())
                        .To.Equal("keep-alive");
                }
            }
        }
    }

    [TestFixture]
    public class Retries
    {
        [TestFixture]
        public class WhenConfiguredRetriesValueIsZero
        {
            [Test]
            public void ShouldOnlyAttemptOnce()
            {
                // Arrange
                var config = CreateDefaultAppSettings();
                Expect(config.MaxUrlFetchRetries)
                    .To.Equal(0);
                var attempts = 0;
                var path = $"/{GetRandomString(2)}.jpg";
                using var server = HttpServerPool.Borrow();
                server.Serve(
                    path,
                    Resources.Data.FluffyCatJpeg,
                    "image/jpeg",
                    (_, _) =>
                    {
                        if (++attempts > 1)
                        {
                            throw new Exception("no image for you");
                        }
                    }
                );

                var sut = Create(config);
                // Act
                Expect(
                    async () =>
                    {
                        await sut.Fetch(
                            server.GetFullUrlFor(path),
                            new Dictionary<string, string>()
                        );
                    }
                ).Not.To.Throw("first hit is always free");

                Expect(
                        async () =>
                        {
                            await sut.Fetch(
                                server.GetFullUrlFor(path),
                                new Dictionary<string, string>()
                            );
                        }
                    ).To.Throw<ImageProviderErrorException>("should fail immediately now")
                    .With.Message.Like("unable to retrieve image");

                // Assert
            }
        }

        [TestFixture]
        public class WhenConfiguredRetriesValueIsNonZero
        {
            [Test]
            public void ShouldAttemptOnceAndRetryUpToLimit()
            {
                // Arrange
                var maxRetries = GetRandomInt(3, 6);
                var config = CreateDefaultAppSettings()
                    .With(o => o.MaxUrlFetchRetries.Returns(maxRetries));
                var attempts = 0;
                var path = $"/{GetRandomString(2)}.jpg";
                using var server = HttpServerPool.Borrow();
                server.Serve(
                    path,
                    Resources.Data.FluffyCatJpeg,
                    "image/jpeg",
                    (_, _) =>
                    {
                        ++attempts;
                        throw new Exception("no image for you");
                    }
                );

                var sut = Create(config);
                // Act
                Expect(
                        async () =>
                        {
                            await sut.Fetch(
                                server.GetFullUrlFor(path),
                                new Dictionary<string, string>()
                            );
                        }
                    ).To.Throw<ImageProviderErrorException>("should fail immediately now")
                    .With.Message.Like("unable to retrieve image");
                Expect(attempts)
                    .To.Equal(maxRetries + 1);
                // Assert
            }
        }
    }

    private static IUrlFetcher Create(
        IAppSettings appSettings = null,
        ILogger<UrlFetcher> logger = null
    )
    {
        return new UrlFetcher(
            appSettings ?? CreateDefaultAppSettings(),
            logger ?? Substitute.For<ILogger<UrlFetcher>>()
        );
    }

    private static IAppSettings CreateDefaultAppSettings()
    {
        var result = Substitute.For<IAppSettings>();
        var _40mb = 40 * 1024 * 1024;
        result.MaxInputImageSize.Returns(_40mb);
        result.MaxOutputImageSize.Returns(_40mb);
        result.MaxImageFetchTimeInMilliseconds.Returns(10000);
        result.MaxUrlFetchRetries.Returns(0);
        result.DomainWhitelist.Returns("*");
        result.EnableTestPage.Returns(true);
        result.EnableConnectionKeepAlive.Returns(false);
        return result;
    }
}
