using System;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using IRAAS.Controllers;
using IRAAS.ImageProcessing;
using IRAAS.Logging;
using IRAAS.Middleware;
using IRAAS.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NExpect;
using NSubstitute;
using static NExpect.Expectations;

namespace IRAAS.Tests;

[TestFixture]
public class TestBootstrapper
{
    [TestCase(typeof(IImageResizer), typeof(ImageResizer))]
    [TestCase(typeof(IImageMimeTypeProvider), typeof(ImageMimeTypeProvider))]
    [TestCase(typeof(IUrlFetcher), typeof(UrlFetcher))]
    [TestCase(typeof(IWhitelist), typeof(Whitelist))]
    [TestCase(typeof(ILogMessageGenerator), typeof(LogMessageGenerator))]
    public void ShouldRegisterSingletonFor_(
        Type serviceType,
        Type implementationType)
    {
        // Arrange
        var container = new Container();
        var sut = Create();
        sut.Bootstrap(container);
        SetupLoggersOn(container);
        // Act
        var result1 = container.Resolve(serviceType);
        var result2 = container.Resolve(serviceType);
        // Assert
        Expect(result1).To.Be.An.Instance.Of(implementationType);
        Expect(result1).To.Be(result2);
    }

    [Test]
    public void ShouldRegisterAppSettingsAsSingleton()
    {
        // Arrange
        var container = new Container();
        var sut = Create();
        // Act
        sut.Bootstrap(container);
        // Assert
        Expect(() => container.Resolve<IAppSettings>())
            .Not.To.Throw();
        var result1 = container.Resolve<IAppSettings>();
        var result2 = container.Resolve<IAppSettings>();
        Expect(result1).Not.To.Be.Null();
        Expect(result1).To.Be(result2);
    }

    [TestCase(typeof(InvalidProcessingOptionsExceptionMiddleware))]
    [TestCase(typeof(NotModifiedExceptionMiddleware))]
    [TestCase(typeof(NotImplementedExceptionMiddleware))]
    [TestCase(typeof(ImageSourceNotAllowedExceptionMiddleware))]
    public void ShouldRegisterNotImplementedExceptionMiddleware(Type middlewareType)
    {
        // Arrange
        var container = new Container();
        var sut = Create();
        // Act
        sut.Bootstrap(container);
        var result = container.Resolve(middlewareType);
        // Assert
        Expect(result).Not.To.Be.Null();
    }

    [TestCase(typeof(IHttpContextAccessor), typeof(HttpContextAccessor))]
    public void ShouldRegisterTransient_(Type serviceType, Type implementationType)
    {
        // Arrange
        var container = new Container();
        container.Register<IServiceScopeFactory, DryIocServiceScopeFactory>(Reuse.Singleton);
        object first;
        object second;
        object third;
        object fourth;
        var sut = Create();
        // Act
        sut.Bootstrap(container);
        using (var scope = container.CreateScope())
        {
            first = scope.ServiceProvider.GetService(serviceType);
            second = scope.ServiceProvider.GetService(serviceType);
        }

        using (var scope = container.CreateScope())
        {
            third = scope.ServiceProvider.GetService(serviceType);
            fourth = scope.ServiceProvider.GetService(serviceType);
        }

        // Assert
        Expect(first).Not.To.Be.Null();
        Expect(second).Not.To.Be.Null();
        Expect(third).Not.To.Be.Null();
        Expect(fourth).Not.To.Be.Null();

        Expect(first).Not.To.Be(second);
        Expect(third).Not.To.Be(fourth);
        Expect(first).Not.To.Be(third);
        Expect(second).Not.To.Be(fourth);

        Expect(first).To.Be.An.Instance.Of(implementationType);
        Expect(second).To.Be.An.Instance.Of(implementationType);
        Expect(third).To.Be.An.Instance.Of(implementationType);
        Expect(fourth).To.Be.An.Instance.Of(implementationType);
    }

    private Bootstrapper Create()
    {
        return new Bootstrapper();
    }

    private void SetupLoggersOn(Container container)
    {
        // asp.net will set up logging -- we have to fake it here
        container.RegisterInstance<ILogger<UrlFetcher>>(
            Substitute.For<ILogger<UrlFetcher>>());
    }
}