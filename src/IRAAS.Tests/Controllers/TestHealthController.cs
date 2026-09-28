using IRAAS.Controllers;
using IRAAS.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using NUnit.Framework;

// ReSharper disable RedundantBoolCompare
namespace IRAAS.Tests.Controllers;

[TestFixture]
public class TestHealthController
{
    [TestCase(Routes.HEALTH)]
    public void ShouldHaveRoute_(
        string expected
    )
    {
        // Arrange
        var sut = typeof(HealthController);

        // Act
        Expect(sut)
            .To.Have.Attribute<RouteAttribute>(
                a => a.Template == Routes.HEALTH
            );

        // Assert
    }

    [TestFixture]
    public class GetHealthTests
    {
        [Test]
        public void ShouldAcceptHttpGetOnEmptySubRoute()
        {
            // Arrange
            var sut = typeof(HealthController);

            // Act
            Expect(sut)
                .To.Have.Method(
                    nameof(HealthController.GetHealth)
                ).With.Route("")
                .Supporting(HttpMethod.Get);
            // Assert
        }

        [Test]
        public void ShouldNotAllowResponseCache()
        {
            // Arrange
            var sut = typeof(HealthController);

            // Act
            Expect(sut)
                .To.Have.Method(
                    nameof(HealthController.GetHealth)
                ).With.Attribute<ResponseCacheAttribute>(
                    a => a.NoStore == true &&
                         a.Location == ResponseCacheLocation.None
                );
            // Assert
        }

        [Test]
        public void ShouldReturnOk()
        {
            // Arrange
            var sut = Create();
            
            // Act
            var result = sut.GetHealth();
            
            // Assert
            Expect(result)
                .To.Be.An.Instance.Of<OkResult>();
            Expect(result.StatusCode)
                .To.Equal(200);
        }
    }

    private static HealthController Create()
    {
        return new();
    }
}