using System;
using System.Net.Http;
using IRAAS.Controllers;
using IRAAS.ImageProcessing;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using NSubstitute;

namespace IRAAS.Tests.Controllers;

[TestFixture]
public class TestTestController
{
    [TestCase("test")]
    public void ControllerShouldHaveRoute_(string expected)
    {
        // Arrange
        // Act
        Expect(typeof(TestController))
            .To.Have.Route(expected);
        // Assert
    }

    [Test]
    public void WhenDisabled_Should404()
    {
        // Arrange
        var appSettings = CreateAppSettings(false);
        // Act
        Expect(() => Create(appSettings))
            .To.Throw<NotImplementedException>();
        // Assert
    }

    [TestFixture]
    public class Test
    {
        [Test]
        public void ShouldHaveEmptyRouteForGET()
        {
            // Arrange
            // Act
            Expect(typeof(TestController))
                .To.Have.Method(nameof(TestController.Test))
                .With.Route("")
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
    public class FileSize
    {
        [Test]
        [Ignore("TODO")]
        public void ShouldFetchUsingUrlFetcher()
        {
            // Arrange
            // Act
            // Assert
        }

        [Test]
        [Ignore("TODO")]
        public void ShouldReportSizeFromFetchedStream()
        {
            // Arrange
            // Act
            // Assert
        }

    }

    private static TestController Create(
        IAppSettings settings,
        IUrlFetcher fetcher = null)
    {
        return new TestController(
            settings,
            fetcher ?? Substitute.For<IUrlFetcher>()
        );
    }

    private static IAppSettings CreateAppSettings(bool enabled)
    {
        var result = Substitute.For<IAppSettings>();
        result.EnableTestPage.Returns(enabled);
        result.MaxImageFetchTimeInMilliseconds.Returns(10000);
        return result;
    }
}
