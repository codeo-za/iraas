using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using NUnit.Framework;
using PeanutButter.SimpleHTTPServer;
using PeanutButter.Utils;
using SixLabors.ImageSharp;

namespace IRAAS.Tests;

/// <summary>
/// Drives the assembled application over real HTTP: real Kestrel, the real
/// middleware pipeline, real routing, real controllers. Everything below this
/// level can be green while the composed app is broken - all three of the
/// defects these cover were invisible to the unit suite.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class IntegrationTests : TestBase
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class ResizingByUrl : TestBase
    {
        [Test]
        public async Task ShouldServeAResizedImageFromAWhitelistedSource()
        {
            // proves the container actually composes: a null IAppSettings
            // registration fails here even though every unit test passes
            // Arrange
            using var upstream = TestEnvironment.BorrowHttpServer();
            var sourceUrl = ServeFluffyCat(upstream.Instance);
            using var app = await StartApp();

            // Act
            using var result = await app.Client.GetAsync(
                $"/?url={Uri.EscapeDataString(sourceUrl)}&width=100"
            );

            // Assert
            Expect(result.StatusCode)
                .To.Equal(HttpStatusCode.OK);
            var body = await result.Content.ReadAsByteArrayAsync();
            var image = Image.Load(body);
            Expect(image.Width)
                .To.Equal(100, "should have been resized to the requested width");
        }

        [Test]
        public async Task ShouldRefuseASourceOutsideTheDomainWhitelist()
        {
            // Arrange
            using var upstream = TestEnvironment.BorrowHttpServer();
            var sourceUrl = ServeFluffyCat(upstream.Instance);
            using var app = await StartApp(
                o => o.DomainWhitelist = "*.somewhere-else.com"
            );

            // Act
            using var result = await app.Client.GetAsync(
                $"/?url={Uri.EscapeDataString(sourceUrl)}&width=100"
            );

            // Assert
            Expect(result.StatusCode)
                .To.Equal(HttpStatusCode.Forbidden);
        }

        [Test]
        public async Task ShouldRejectAMalformedUrlWithoutFallingOver()
        {
            // Arrange
            using var app = await StartApp();

            // Act
            using var result = await app.Client.GetAsync("/?url=not%20a%20url");

            // Assert
            Expect(result.StatusCode)
                .To.Equal(
                    HttpStatusCode.BadRequest,
                    "an unparseable url should not reach the unhandled-exception handler"
                );
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class TheTestPage : TestBase
    {
        // the gate decides by path string; routing normalises. Every spelling
        // routing accepts has to be one the gate also recognises.
        [TestCase("/test")]
        [TestCase("/test/")]
        [TestCase("/TEST")]
        public async Task ShouldBeUnreachableWhenDisabled_(string path)
        {
            // Arrange
            using var app = await StartApp(o => o.EnableTestPage = false);

            // Act
            using var result = await app.Client.GetAsync(path);

            // Assert
            Expect(result.StatusCode)
                .To.Equal(HttpStatusCode.NotFound);
        }

        [TestCase("/size")]
        [TestCase("/size/")]
        public async Task ShouldNotExposeTheSizeEndpointWhenDisabled_(string path)
        {
            // Arrange
            using var upstream = TestEnvironment.BorrowHttpServer();
            var sourceUrl = ServeFluffyCat(upstream.Instance);
            using var app = await StartApp(o => o.EnableTestPage = false);

            // Act
            using var result = await app.Client.GetAsync(
                $"{path}?url={Uri.EscapeDataString(sourceUrl)}"
            );

            // Assert
            Expect(result.StatusCode)
                .To.Equal(
                    HttpStatusCode.NotFound,
                    "a disabled endpoint should look absent, not merely refuse"
                );
        }

        [Test]
        public async Task ShouldServeTheTestPageWhenEnabled()
        {
            // Arrange
            using var app = await StartApp(o => o.EnableTestPage = true);

            // Act
            using var result = await app.Client.GetAsync("/test");

            // Assert
            Expect(result.StatusCode)
                .To.Equal(HttpStatusCode.OK);
        }

        [Test]
        public async Task ShouldReportImageSizeWhenEnabled()
        {
            // Arrange
            using var upstream = TestEnvironment.BorrowHttpServer();
            var sourceUrl = ServeFluffyCat(upstream.Instance);
            var expectedLength = Resources.Data.FluffyCatJpeg.Length;
            using var app = await StartApp(o => o.EnableTestPage = true);

            // Act
            using var result = await app.Client.GetAsync(
                $"/size?url={Uri.EscapeDataString(sourceUrl)}"
            );

            // Assert
            Expect(result.StatusCode)
                .To.Equal(HttpStatusCode.OK);
            var reported = long.Parse(await result.Content.ReadAsStringAsync());
            Expect(reported)
                .To.Equal(expectedLength);
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class ResizingByPostedImageData : TestBase
    {
        [Test]
        public async Task ShouldRefusePostsWhenNotEnabled()
        {
            // Arrange
            var imageData = Resources.Data.FluffyCatJpeg;
            using var app = await StartApp(o => o.AllowPostRequests = false);

            // Act
            using var result = await Post(app, imageData);

            // Assert
            Expect(result.StatusCode)
                .To.Equal(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task ShouldRefusePostsBearingAnUnknownToken()
        {
            // Arrange
            var token = $"{Guid.NewGuid()}";
            var imageData = Resources.Data.FluffyCatJpeg;
            using var app = await StartApp(
                o =>
                {
                    o.AllowPostRequests = true;
                    o.PostAuthTokens = token;
                }
            );

            // Act
            using var result = await Post(
                app,
                imageData,
                token: $"{Guid.NewGuid()}"
            );

            // Assert
            Expect(result.StatusCode)
                .To.Equal(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task ShouldResizePostedImageDataForAKnownToken()
        {
            // Arrange
            var token = $"{Guid.NewGuid()}";
            var imageData = Resources.Data.FluffyCatJpeg;
            using var app = await StartApp(
                o =>
                {
                    o.AllowPostRequests = true;
                    o.PostAuthTokens = token;
                }
            );

            // Act
            using var result = await Post(
                app,
                imageData,
                token: token,
                width: 100
            );

            // Assert
            Expect(result.StatusCode)
                .To.Equal(HttpStatusCode.OK);
            var image = Image.Load(await result.Content.ReadAsByteArrayAsync());
            Expect(image.Width)
                .To.Equal(100);
        }

        [Test]
        public async Task ShouldRejectImageDataOverMaxInputImageSizeWithBadRequest()
        {
            // sits inside the derived kestrel body limit (x1.5) but over our own
            // limit, so the request reaches application code and gets a 400
            // Arrange
            var maxInputImageSize = 200_000;
            using var app = await StartApp(
                o =>
                {
                    o.AllowPostRequests = true;
                    o.PostAuthTokens = "*";
                    o.MaxInputImageSize = maxInputImageSize;
                }
            );

            // Act
            using var result = await Post(
                app,
                GetRandomBytes(maxInputImageSize + 1, maxInputImageSize + 100)
            );

            // Assert
            Expect(result.StatusCode)
                .To.Equal(HttpStatusCode.BadRequest);
        }

        [Test]
        public async Task ShouldNotReportSuccessForGrosslyOversizedPayloads()
        {
            // beyond the derived kestrel body limit, so kestrel aborts the read
            // before any of our code runs. Asserting "not a success" rather than
            // a specific code: 413 would be ideal, 500 is acceptable, but the
            // client must not be told the upload worked.
            // Arrange
            var maxInputImageSize = 200_000;
            using var app = await StartApp(
                o =>
                {
                    o.AllowPostRequests = true;
                    o.PostAuthTokens = "*";
                    o.MaxInputImageSize = maxInputImageSize;
                }
            );

            // Act
            using var result = await Post(
                app,
                GetRandomBytes(maxInputImageSize * 3, maxInputImageSize * 3 + 100)
            );

            // Assert
            Expect(result.IsSuccessStatusCode)
                .To.Be.False(
                    $"an over-limit upload reported {(int)result.StatusCode} {result.StatusCode}"
                );
        }

        private static async Task<HttpResponseMessage> Post(
            RunningApp app,
            byte[] imageData,
            string token = null,
            int? width = null
        )
        {
            var payload = new Dictionary<string, object>
            {
                ["imageData"] = imageData
            };
            if (width is not null)
            {
                payload["width"] = width;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "/")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    new MediaTypeHeaderValue("application/json")
                )
            };
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await app.Client.SendAsync(request);
        }
    }

    private static string ServeFluffyCat(IHttpServer server)
    {
        var path = $"/{GetRandomString(8)}.jpg";
        server.ServeFile(
            path,
            Resources.Data.FluffyCatJpeg,
            "image/jpeg"
        );
        return server.GetFullUrlFor(path);
    }

    private static Task<RunningApp> StartApp(
        Action<AppOptions> configure = null
    )
    {
        var options = new AppOptions();
        configure?.Invoke(options);
        return RunningApp.Start(options);
    }

    public class AppOptions
    {
        public bool EnableTestPage { get; set; }
        public bool AllowPostRequests { get; set; }
        public string PostAuthTokens { get; set; } = "";
        public string DomainWhitelist { get; set; } = "*";
        public int MaxInputImageSize { get; set; } = 41943040;
    }

    /// <summary>
    /// The real application, listening on a real port, configured from a real
    /// appsettings.json - which is the only way to exercise the host wiring and
    /// the kestrel limits, since both are established while the host is built.
    /// </summary>
    private class RunningApp : IDisposable
    {
        public HttpClient Client { get; private init; }

        private IWebHost _host;
        private AutoTempFolder _folder;
        private AutoResetter<string> _cwd;

        public static async Task<RunningApp> Start(AppOptions options)
        {
            var folder = new AutoTempFolder();
            var port = PortFinder.FindOpenPort();
            var baseUrl = $"http://127.0.0.1:{port}";
            File.WriteAllText(
                Path.Combine(folder.Path, AppSettingsProvider.BASE_CONFIG),
                GenerateAppSettings(options, baseUrl)
            );

            var cwd = new AutoResetter<string>(
                () =>
                {
                    var prior = Directory.GetCurrentDirectory();
                    Directory.SetCurrentDirectory(folder.Path);
                    return prior;
                },
                Directory.SetCurrentDirectory
            );

            try
            {
                var host = Program.CreateWebHostBuilder([]).Build();
                await host.StartAsync();
                return new RunningApp
                {
                    _host = host,
                    _folder = folder,
                    _cwd = cwd,
                    Client = new HttpClient
                    {
                        BaseAddress = new Uri(baseUrl),
                        Timeout = TimeSpan.FromSeconds(30)
                    }
                };
            }
            catch
            {
                cwd.Dispose();
                folder.Dispose();
                throw;
            }
        }

        private static string GenerateAppSettings(
            AppOptions options,
            string baseUrl
        )
        {
            return $$"""
                     {
                       "Urls": "{{baseUrl}}",
                       "Logging": {
                         "LogLevel": {
                           "Default": "None",
                           "IRAAS": "None"
                         }
                       },
                       "Settings": {
                         "MaxInputImageSize": "{{options.MaxInputImageSize}}",
                         "EnableTestPage": "{{options.EnableTestPage.ToString().ToLowerInvariant()}}",
                         "AllowPostRequests": "{{options.AllowPostRequests.ToString().ToLowerInvariant()}}",
                         "PostAuthTokens": "{{options.PostAuthTokens}}",
                         "DomainWhitelist": "{{options.DomainWhitelist}}",
                         "MaxImageFetchTimeInMilliseconds": "10000"
                       }
                     }
                     """;
        }

        public void Dispose()
        {
            Client?.Dispose();
            try
            {
                _host?.StopAsync().Wait(TimeSpan.FromSeconds(10));
            }
            catch
            {
                // a host that will not stop cleanly shouldn't fail the assertion
            }

            _host?.Dispose();
            _cwd?.Dispose();
            _folder?.Dispose();
            _host = null;
            _cwd = null;
            _folder = null;
        }
    }
}
