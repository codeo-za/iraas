using DryIoc;
using IRAAS.Tests.ImageProcessing;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using NUnit.Framework;

namespace IRAAS.Tests;

public class CompositionRootTests
{
    [Test]
    public void ShouldBootstrapOnProvidedContainer()
    {
        // Arrange
        var container = new Container();
        var appSettings = Substitute.For<IAppSettings>()
            .WithDefaultSettings();
        // Act
        Create(container, appSettings);
        
        // Assert
        Expect(container.Resolve<IAppSettings>())
            .To.Be(appSettings);
        Expect(container.Resolve<IHttpContextAccessor>())
            .Not.To.Be.Null();
    }

    private static CompositionRoot Create(
        IContainer container,
        IAppSettings appSettings
    )
    {
        return new(container, appSettings);
    }
}