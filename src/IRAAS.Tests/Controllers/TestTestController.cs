using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Threading.Tasks;
using IRAAS.Controllers;
using IRAAS.ImageProcessing;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using NSubstitute;
using PeanutButter.TestUtils.AspNetCore.Builders;
using PeanutButter.Utils;

namespace IRAAS.Tests.Controllers;

[TestFixture]
public class TestTestController : TestBase
{
    [TestCase("")]
    public void ControllerShouldHaveRoute_(string expected)
    {
        // Arrange
        // Act
        Expect(typeof(TestController))
            .To.Have.Route(expected);
        // Assert
    }

    [TestFixture]
    public class Test : TestBase
    {
        [TestCase("test")]
        public void ShouldHaveRouteForGET_(
            string expected
        )
        {
            // Arrange
            // Act
            Expect(typeof(TestController))
                .To.Have.Method(nameof(TestController.Test))
                .With.Route(expected)
                .Supporting(HttpMethod.Get);
            // Assert
        }

        [Test]
        public void WhenEnabled_ShouldReturnViewResult()
        {
            // Arrange
            var appSettings = CreateAppSettings(true);
            var sut = Create(appSettings);
            // Act
            var result = sut.Test();
            // Assert
            Expect(result)
                .Not.To.Be.Null();
            Expect(result)
                .To.Be.An.Instance.Of<ViewResult>();
        }
    }

    [TestFixture]
    public class FileSize : TestBase
    {
        [TestCase("size")]
        public void ShouldHaveRoute_(
            string expected
        )
        {
            // Arrange
            // Act
            Expect(typeof(TestController))
                .To.Have.Method(nameof(TestController.FileSize))
                .With.Route(expected)
                .Supporting(HttpMethod.Get);
            // Assert
        }

        [Test]
        [Ignore("WIP: fixing an issue in PB where request objects are being clobbered")]
        public async Task ShouldFetchUsingUrlFetcher()
        {
            // Arrange
            var url = GetRandomHttpsUrlWithPath();
            var data = GetRandomBytes();
            var stream = new MemoryStream(data);
            var fetchResult = new StreamAndHeaders(
                stream,
                new Dictionary<string, string>()
            );
            var fetcher = Substitute.For<IUrlFetcher>()
                .With(
                    o => o.Fetch(url, Arg.Any<Dictionary<string, string>>())
                        .Returns(_ => fetchResult)
                );
            var headers = GetRandom<Dictionary<string, string>>();
            Expect(headers)
                .Not.To.Be.Empty();
            var req = HttpRequestBuilder.Create()
                .WithMethod(HttpMethod.Get)
                .WithRandomUrl()
                .WithHeaders(headers)
                .Build();
            var ctx = ControllerContextBuilder.Create()
                .WithRequest(req)
                .Build();
            var sut = Create(
                Substitute.For<IAppSettings>(),
                fetcher,
                ctx
            );
            Expect(sut.Request)
                .To.Be(ctx.HttpContext.Request);


            // Act
            var result = await sut.FileSize(url);

            // Assert
            Expect(result)
                .To.Equal(data.Length);
            await Expect(fetcher)
                .To.Have.Received(1)
                .Fetch(
                    Arg.Is<string>(
                        s => s.Equals(url, StringComparison.OrdinalIgnoreCase)
                    ),
                    Arg.Is<Dictionary<string, string>>(
                        o => o.DeepEquals(headers)
                    )
                );
        }

        [Test]
        public async Task ShouldReportSizeFromFetchedStream()
        {
            // Arrange
            var data = GetRandomBytes();
            var url = GetRandomHttpsUrl();
            var resource = new StreamAndHeaders(data);
            var fetcher = Substitute.For<IUrlFetcher>()
                .With(
                    o => o.Fetch(url, Arg.Any<Dictionary<string, string>>())
                        .Returns(_ => resource)
                );
            var sut = Create(
                Substitute.For<IAppSettings>(),
                fetcher
            );

            // Act
            var result = await sut.FileSize(url);

            // Assert
            Expect(result)
                .To.Equal(data.Length);
            await Expect(fetcher)
                .To.Have.Received(1)
                .Fetch(
                    url,
                    Arg.Is<IDictionary<string, string>>(
                        o => o.DeepEquals(sut.Request.Headers.ToDictionary())
                    )
                );
        }
    }

    private static TestController Create(
        IAppSettings settings,
        IUrlFetcher fetcher = null,
        ControllerContext ctx = null
    )
    {
        return new TestController(
            settings,
            fetcher ?? Substitute.For<IUrlFetcher>()
        )
        {
            ControllerContext = ctx ?? ControllerContextBuilder.Create()
                .WithRequestHeader(GetRandomString(10), GetRandomString(10))
                .Build()
        };
    }

    private static IAppSettings CreateAppSettings(bool enabled)
    {
        var result = Substitute.For<IAppSettings>();
        result.EnableTestPage.Returns(enabled);
        result.MaxImageFetchTimeInMilliseconds.Returns(10000);
        return result;
    }
}