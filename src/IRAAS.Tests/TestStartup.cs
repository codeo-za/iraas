using System.IO;
using IRAAS.ImageProcessing;
using IRAAS.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using PeanutButter.Utils;

namespace IRAAS.Tests;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class TestStartup : TestBase
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class KestrelMaxRequestBodySize : TestBase
    {
        [Test]
        public void ShouldBeDerivedFromMaxInputImageSizeWhenNotConfigured()
        {
            // Arrange
            // -> POST payloads carry the image base64-encoded (4/3 inflation) plus
            //    the resize parameters, so the transport limit has to sit above
            //    MaxInputImageSize or that setting can never be reached
            var maxInputImageSize = 10_000_000;
            var expected = 15_000_000L; // maxInputImageSize + 50%
            using var tempFolder = new AutoTempFolder();
            WriteAppSettings(
                tempFolder,
                maxInputImageSize
            );
            using var _ = InFolder(tempFolder.Path);
            using var host = BuildHost();

            // Act
            var result = MaxRequestBodySizeOf(host);

            // Assert
            Expect(result)
                .To.Equal(expected);
        }

        [Test]
        public void ShouldFallOverToExplicitKestrelConfigurationWhenProvided()
        {
            // Arrange
            var maxInputImageSize = 10_000_000;
            var derived = 15_000_000L;
            var configured = 99_999_999L;
            Expect(configured)
                .Not.To.Equal(derived, "the test can't tell the two apart otherwise");
            using var tempFolder = new AutoTempFolder();
            WriteAppSettings(
                tempFolder,
                maxInputImageSize,
                configured
            );
            using var _ = InFolder(tempFolder.Path);
            using var host = BuildHost();

            // Act
            var result = MaxRequestBodySizeOf(host);

            // Assert
            Expect(result)
                .To.Equal(configured);
            Expect(result)
                .Not.To.Equal(derived, "explicit kestrel config should win over the derived value");
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class Composition : TestBase
    {
        [Test]
        public void ShouldResolveAppSettingsFromTheComposedHost()
        {
            // Arrange
            using var tempFolder = new AutoTempFolder();
            var maxInputImageSize = 10_000_000;
            WriteAppSettings(
                tempFolder,
                maxInputImageSize
            );
            using var _ = InFolder(tempFolder.Path);
            using var host = BuildHost();

            // Act
            var result = host.Services.GetService<IAppSettings>();

            // Assert
            Expect(result)
                .Not.To.Be.Null(
                    "CompositionRoot must hand the settings it was given to Bootstrapper"
                );
            Expect(result.MaxInputImageSize)
                .To.Equal(
                    maxInputImageSize,
                    "the registered settings should be the ones built from config"
                );
        }

        [TestCase(typeof(IImageResizer))]
        [TestCase(typeof(IUrlFetcher))]
        [TestCase(typeof(IWhitelist))]
        public void ShouldResolveServicesDependingOnAppSettings_(
            System.Type serviceType
        )
        {
            // everything below takes IAppSettings, so a null registration
            // surfaces here as a NullReferenceException at resolve time
            // rather than anywhere near the mistake that caused it

            // Arrange
            using var tempFolder = new AutoTempFolder();
            WriteAppSettings(
                tempFolder,
                10_000_000
            );
            using var _ = InFolder(tempFolder.Path);
            using var host = BuildHost();

            // Act
            var result = host.Services.GetService(serviceType);

            // Assert
            Expect(result)
                .Not.To.Be.Null();
        }
    }

    private static IWebHost BuildHost()
    {
        return Program.CreateWebHostBuilder([]).Build();
    }

    private static long? MaxRequestBodySizeOf(IWebHost host)
    {
        return host.Services
            .GetRequiredService<IOptions<KestrelServerOptions>>()
            .Value
            .Limits
            .MaxRequestBodySize;
    }

    private static void WriteAppSettings(
        AutoTempFolder folder,
        int maxInputImageSize,
        long? kestrelMaxRequestBodySize = null
    )
    {
        var kestrelSection = kestrelMaxRequestBodySize is null
            ? ""
            : $$"""
                  "Kestrel": {
                    "Limits": {
                      "MaxRequestBodySize": "{{kestrelMaxRequestBodySize}}"
                    }
                  },
                """;
        File.WriteAllText(
            Path.Combine(folder.Path, AppSettingsProvider.BASE_CONFIG),
            $$"""
              {
                "Urls": "http://127.0.0.1:5000",
              {{kestrelSection}}
                "Settings": {
                  "MaxInputImageSize": "{{maxInputImageSize}}"
                }
              }
              """
        );
    }

    /// <summary>
    /// Configuration is built against the current working directory and then cached,
    /// so a host must be built from the folder holding the appsettings.json it should
    /// read. TestBase clears the caches around every test, so moving is all that is
    /// needed here - but only one host per test.
    /// </summary>
    private static AutoResetter<string> InFolder(string path)
    {
        return new AutoResetter<string>(
            () =>
            {
                var prior = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(path);
                return prior;
            },
            Directory.SetCurrentDirectory
        );
    }
}
