using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using IRAAS.ImageProcessing;
using IRAAS.Middleware;
using IRAAS.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
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

    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class MiddlewareTests : TestBase
    {
        /// <summary>
        /// The order the pipeline is assembled in is load-bearing, and several of
        /// these constraints were learned the hard way:
        ///  - ProductionFallbackExceptionHandlerMiddleware must be outermost; it is
        ///    the last-resort 500 for anything nothing else claimed.
        ///  - BadHttpRequestExceptionMiddleware must sit OUTSIDE ConcurrencyMiddleware.
        ///    Concurrency swaps the response body for a buffer and only restores it in
        ///    its finally, so a handler inside it would write to the discarded buffer.
        ///  - ArgumentExceptionMiddleware must sit OUTSIDE ArgumentNullExceptionMiddleware.
        ///    ArgumentNullException derives from ArgumentException, so the general
        ///    handler swallows both if it is the inner one.
        ///  - AuthorizationMiddleware must be innermost of these, so the exceptions it
        ///    throws are translated by the handlers above it rather than escaping.
        /// Re-ordering is not necessarily wrong - but it should be deliberate, so if
        /// this test fails, work out which of the above you are changing before
        /// updating the list.
        /// </summary>
        private static readonly Type[] ExpectedPipeline =
        [
            typeof(ProductionFallbackExceptionHandlerMiddleware),
            typeof(MaxClientsMiddleware),
            typeof(BadHttpRequestExceptionMiddleware),
            typeof(ConcurrencyMiddleware),
            typeof(InvalidProcessingOptionsExceptionMiddleware),
            typeof(NotImplementedExceptionMiddleware),
            typeof(ImageSourceNotAllowedExceptionMiddleware),
            typeof(ImageProviderErrorMiddleware),
            typeof(RedirectTimedOutRequestsMiddleware),
            typeof(NotModifiedExceptionMiddleware),
            typeof(ArgumentExceptionMiddleware),
            typeof(ArgumentNullExceptionMiddleware),
            typeof(AuthorizationMiddleware)
        ];

        [Test]
        public async Task ShouldRunProjectMiddlewareInTheExpectedOrder()
        {
            // Arrange
            // Act
            var result = await ObserveMiddlewarePipeline();

            // Assert
            Expect(result)
                .To.Equal(ExpectedPipeline);
        }

        [Test]
        public async Task ShouldRunEveryMiddlewareTheProjectDefines()
        {
            // catches the "wrote the middleware, forgot to wire it up" mistake,
            // which is invisible to a unit test of the middleware itself
            // Arrange
            var defined = typeof(Startup).Assembly
                .GetTypes()
                .Where(t => t.IsClass)
                .Where(t => !t.IsAbstract)
                .Where(t => typeof(IMiddleware).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToArray();
            Expect(defined)
                .Not.To.Be.Empty();

            // Act
            var result = await ObserveMiddlewarePipeline();

            // Assert
            Expect(result.OrderBy(t => t.Name).ToArray())
                .To.Equal(
                    defined,
                    () => "every IMiddleware in the IRAAS assembly should be in the pipeline:\n" +
                        $"  missing: {Describe(defined.Except(result))}\n" +
                        $"  unexpected: {Describe(result.Except(defined))}"
                );
        }

        private static string Describe(IEnumerable<Type> types)
        {
            var names = types.Select(t => t.Name).ToArray();
            return names.Any()
                ? string.Join(", ", names)
                : "(none)";
        }

        /// <summary>
        /// Middleware registered via UseMiddleware&lt;T&gt; is instantiated through
        /// IMiddlewareFactory, in pipeline order, on the way in. Swapping in a
        /// recording factory therefore observes the real, assembled order rather
        /// than re-reading the source.
        /// </summary>
        private static async Task<Type[]> ObserveMiddlewarePipeline()
        {
            var recorded = new List<Type>();
            using var tempFolder = new AutoTempFolder();
            WriteAppSettings(
                tempFolder,
                10_000_000
            );
            using var _ = InFolder(tempFolder.Path);
            using var host = Program.CreateWebHostBuilder([])
                .ConfigureServices(
                    services => services.AddSingleton<IMiddlewareFactory>(
                        provider => new RecordingMiddlewareFactory(provider, recorded)
                    )
                )
                .Build();
            await host.StartAsync();
            try
            {
                using var client = new HttpClient
                {
                    BaseAddress = new Uri(BaseUrlOf(host))
                };
                // any request reaching the innermost middleware will do - every
                // middleware ahead of it is constructed on the way in, so this
                // does not need to be a request that actually succeeds
                using var response = await client.GetAsync("/");
                Expect(response.StatusCode)
                    .To.Equal(
                        HttpStatusCode.NotFound,
                        "a url-less resize request should be rejected by AuthorizationMiddleware"
                    );
            }
            finally
            {
                await host.StopAsync();
            }

            return recorded.ToArray();
        }

        private static string BaseUrlOf(IWebHost host)
        {
            return host.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()!
                .Addresses
                .First();
        }

        private class RecordingMiddlewareFactory : IMiddlewareFactory
        {
            private readonly IServiceProvider _provider;
            private readonly List<Type> _recorded;

            public RecordingMiddlewareFactory(
                IServiceProvider provider,
                List<Type> recorded
            )
            {
                _provider = provider;
                _recorded = recorded;
            }

            public IMiddleware Create(Type middlewareType)
            {
                _recorded.Add(middlewareType);
                return _provider.GetRequiredService(middlewareType) as IMiddleware;
            }

            public void Release(IMiddleware middleware)
            {
            }
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
        // most of these tests only Build() the host, but the ones that Start() it
        // need a port of their own - a fixed one would collide with a locally-running
        // instance or another test run on the same machine
        File.WriteAllText(
            Path.Combine(folder.Path, AppSettingsProvider.BASE_CONFIG),
            $$"""
              {
                "Urls": "http://127.0.0.1:{{PortFinder.FindOpenPort()}}",
                "Logging": {
                  "LogLevel": {
                    "Default": "None",
                    "IRAAS": "None"
                  }
                },
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
