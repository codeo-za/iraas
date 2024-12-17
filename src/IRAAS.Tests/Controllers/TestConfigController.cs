using System.Net.Http;
using IRAAS.Controllers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PeanutButter.DuckTyping.Extensions;
using PeanutButter.JObjectExtensions;

namespace IRAAS.Tests.Controllers;

[TestFixture]
public class TestConfigController
{
    [TestCase("config")]
    public void ControllerShouldHaveRoute_(string expected)
    {
        // Arrange
        // Act
        // Assert
        Expect(typeof(ConfigController))
            .To.Have.Route(expected);
    }

    [TestFixture]
    public class Config
    {
        [Test]
        public void ShouldHaveEmptyRouteForGet()
        {
            // Arrange
            // Act
            // Assert
            Expect(typeof(ConfigController))
                .To.Have.Method(nameof(ConfigController.DumpConfig))
                .With.Route("")
                .Supporting(HttpMethod.Get);
        }

        [Test]
        public void ShouldDumpConfigAsJsonString()
        {
            // Arrange
            var config = GetRandom<IAppSettings>();
            var sut = Create(config);
            // Act
            var result = sut.DumpConfig();
            // Assert
            var deserialized = (JObject)JsonConvert.DeserializeObject(result);
            Expect(deserialized.ToDictionary().ForceFuzzyDuckAs<IAppSettings>())
                .To.Deep.Equal(config);
        }
    }

    private static ConfigController Create(IAppSettings config)
    {
        return new ConfigController(config);
    }
}