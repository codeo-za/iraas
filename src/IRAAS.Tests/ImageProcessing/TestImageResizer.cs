using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IRAAS.Exceptions;
using IRAAS.ImageProcessing;
using Microsoft.Extensions.Logging;
using NExpect.Interfaces;
using NExpect.MatcherLogic;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.SimpleHTTPServer;
using PeanutButter.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace IRAAS.Tests.ImageProcessing;

[TestFixture]
public class TestImageResizer: TestBase
{
    [Test]
    public void ShouldImplement_IImageProcessor()
    {
        // Arrange
        // Act
        Expect(typeof(ImageResizer))
            .To.Implement<IImageResizer>();
        // Assert
    }

    private static readonly Dictionary<string, string> NoHeaders
        = new Dictionary<string, string>();

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void ShouldThrowForInvalidUrl_(string url)
    {
        // Arrange
        var sut = Create(Substitute.For<IUrlFetcher>());
        // Act
        Expect(
            () => sut.Resize(
                new ImageResizeOptions()
                {
                    Url = url
                },
                NoHeaders
            )
        ).To.Throw<InvalidProcessingOptionsException>();
        // Assert
    }

    [Test]
    public void ShouldGetImageStreamFromImageFetcher()
    {
        // Arrange
        var fetcher = CreateFetcherFor(Resources.Data.FluffyCatJpeg);
        var sut = Create(fetcher);
        var options = new ImageResizeOptions()
        {
            Url = GetRandomHttpUrlWithPath()
        };
        // Act
        sut.Resize(options, NoHeaders);
        // Assert
        Expect(fetcher).To.Have.Received(1)
            .Fetch(options.Url, Arg.Any<IDictionary<string, string>>());
    }

    [Test]
    public void ShouldPassHeadersThroughToImageFetcher()
    {
        // Arrange
        var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp);
        var sut = Create(fetcher);
        var headers = new Dictionary<string, string>();
        headers[GetRandomString(10)] = GetRandomString(10);
        headers[GetRandomString(10)] = GetRandomString(10);
        headers[GetRandomString(10)] = GetRandomString(10);
        var options = new ImageResizeOptions()
        {
            Url = GetRandomHttpUrlWithPath()
        };
        // Act
        sut.Resize(options, headers);
        // Assert
        Expect(fetcher).To.Have.Received(1)
            .Fetch(
                options.Url,
                Arg.Is<IDictionary<string, string>>(
                    dict => dict.IsEquivalentTo(headers)
                )
            );
    }

    [Test]
    public async Task ShouldPassBackHeadersFromImageFetcher()
    {
        // Arrange
        var requestHeaders = new Dictionary<string, string>
        {
            [GetRandomString(10)] = GetRandomString(10),
            [GetRandomString(10)] = GetRandomString(10),
            [GetRandomString(10)] = GetRandomString(10)
        };
        var responseHeaders = new Dictionary<string, string>
        {
            [GetRandomString(10)] = GetRandomString(10),
            [GetRandomString(10)] = GetRandomString(10),
            [GetRandomString(10)] = GetRandomString(10)
        };
        var options = new ImageResizeOptions()
        {
            Url = GetRandomHttpUrlWithPath()
        };
        var fetcher = CreateFetcherFor(
            Resources.Data.FluffyCatBmp,
            headers: responseHeaders
        );
        var sut = Create(fetcher);
        // Act
        var result = await sut.Resize(options, requestHeaders);
        // Assert
        var scrubbedOfTimings = result.Headers.Where(
            h => !h.Key.StartsWith(TimingHeaders.PREFIX)
        ).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Expect(scrubbedOfTimings)
            .To.Be.Equivalent.To(responseHeaders);
        // Expect(scrubbedOfTimings.IsEquivalentTo(responseHeaders))
        //     .To.Be.True();
    }

    [Test]
    public void ShouldThrowNotSupportedIfImageStreamUnrecognised()
    {
        // Arrange
        var data = GetRandomBytes(1024, 2048);
        var fetcher = CreateFetcherFor(data);
        var options = new ImageResizeOptions()
        {
            Url = GetRandomHttpUrlWithPath()
        };
        var sut = Create(fetcher);
        // Act
        Expect(() => sut.Resize(options, NoHeaders))
            .To.Throw<NotSupportedException>();
        // Assert
    }

    [TestFixture]
    public class BitmapInputJpegOutput: TestBase
    {
        // note that bmp source for testing is only 800x600 because
        // bitmaps are large binary blobs I'd rather keep out of the repo
        [Test]
        public async Task GivenOnlyUrl_ShouldFetchAndReOptimiseAsJpegAtSameSizeWithQuality85()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(
                Resources.Data.FluffyCatBmp,
                url
            );
            var sut = Create(fetcher);
            var sourceImage = Resources.Images.FluffyCatBmp;
            var expectedWidth = sourceImage.Width;
            var expectedHeight = sourceImage.Height;
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Url = url
                },
                NoHeaders
            );
            // Assert
            Expect(result)
                .Not.To.Be.Null();
            await Expect(fetcher).To.Have.Received(1).Fetch(url, Arg.Any<IDictionary<string, string>>());
            var image = await Image.LoadAsync(result.Stream);
            Expect(image.Metadata.GetFormatMetadata(JpegFormat.Instance).Quality)
                .To.Equal(85);
            Expect(image)
                .To.Have.Width(expectedWidth);
            Expect(image)
                .To.Have.Height(expectedHeight);
        }

        [Test]
        public async Task GivenQuality_ShouldOptimiseAtGivenQuality()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
            var expected = GetRandomInt(40, 60);
            var sut = Create(fetcher);
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Url = url,
                    Quality = expected
                },
                NoHeaders
            );
            // Assert
            var image = await Image.LoadAsync(result.Stream);
            var quality = image.Metadata
                .GetFormatMetadata(JpegFormat.Instance)
                .Quality;
            Expect(quality)
                .To.Equal(expected);
        }

        [Test]
        public async Task GivenNoResizeMode_ShouldResizeToMax()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
            var sut = Create(fetcher);
            // source image is 800x600
            // -> result should be 400px wide
            //   -> result should be 1200 / 4 = 300px tall
            var expectedWidth = 400;
            var expectedHeight = 300;
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Width = 400,
                    Height = 400,
                    Url = url
                },
                NoHeaders
            );
            // Assert
            var image = await Image.LoadAsync(result.Stream);
            Expect(image)
                .To.Have.Width(expectedWidth);
            Expect(image)
                .To.Have.Height(expectedHeight);
        }

        [Test]
        public async Task GivenResizeMode_ShouldUseIt()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
            var sut = Create(fetcher);
            // source image is 800x600
            // -> result should be 300px tall
            //   -> result should be 1600 / 4 = 400px
            var expectedWidth = 400;
            var expectedHeight = 300;
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Width = 300,
                    Height = 300,
                    Url = url,
                    ResizeMode = ResizeMode.Min
                },
                NoHeaders
            );
            // Assert
            var image = await Image.LoadAsync(result.Stream);
            Expect(image)
                .To.Have.Width(expectedWidth);
            Expect(image)
                .To.Have.Height(expectedHeight);
        }

        [Test]
        public async Task GivenOnlyWidth_ShouldResizeToFitWidthAndMaintainAspectRatio()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
            var sut = Create(fetcher);
            // source image is 800x600
            // -> if width is specified as 400, implies height of 300
            var expectedWidth = 400;
            var expectedHeight = 300;
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Width = expectedWidth,
                    Url = url,
                },
                NoHeaders
            );
            // Assert
            var image = await Image.LoadAsync(result.Stream);
            Expect(image)
                .To.Have.Width(expectedWidth);
            Expect(image)
                .To.Have.Height(expectedHeight);
        }

        [Test]
        public async Task GivenOnlyWidthAndDevicePixelRatio_ShouldResizeToFitWidthAndMaintainAspectRatio()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
            var sut = Create(fetcher);
            // source image is 800x600
            // -> if width is specified as 400, implies height of 300
            var effectiveWidth = 400;
            var devicePixelRatio = GetRandomDevicePixelRatio();
            var expectedWidth = (int) Math.Ceiling(effectiveWidth * devicePixelRatio);
            var expectedHeight = (int) Math.Ceiling(300 * devicePixelRatio);
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Width = effectiveWidth,
                    DevicePixelRatio = devicePixelRatio,
                    Url = url,
                },
                NoHeaders
            );
            // Assert
            var image = await Image.LoadAsync(result.Stream);
            Expect(image)
                .To.Have.Width(expectedWidth, DumpDebugInfo);
            Expect(image)
                .To.Have.Height(expectedHeight, DumpDebugInfo);

            string DumpDebugInfo()
            {
                return new
                {
                    expectedWidth,
                    expectedHeight,
                    effectiveWidth,
                    devicePixelRatio
                }.Stringify();
            }
        }

        [Test]
        public async Task GivenOnlyWidthAndDevicePixelRatioResultingInExplodingImage_NotResizeBeyondOriginalSize()
        {
            // use-case: we have an image which is, eg 800x600
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
            var sut = Create(fetcher);
            // source image is 800x600
            // -> if width is specified as 400, implies height of 300
            var effectiveWidth = 400;
            // request is basically to half the image
            // but devicePixelRatio is 3x, so effective
            // size is 1.5x -- and we don't stretch by default
            var devicePixelRatio = 3.0M;
            var expectedWidth = 800;
            var expectedHeight = 600;
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Width = effectiveWidth,
                    DevicePixelRatio = devicePixelRatio,
                    Url = url,
                },
                NoHeaders
            );
            // Assert
            var image = await Image.LoadAsync(result.Stream);
            Expect(image)
                .To.Have.Width(expectedWidth, DumpDebugInfo);
            Expect(image)
                .To.Have.Height(expectedHeight, DumpDebugInfo);

            string DumpDebugInfo()
            {
                return new
                {
                    expectedWidth,
                    expectedHeight,
                    effectiveWidth,
                    devicePixelRatio
                }.Stringify();
            }
        }

        [Test]
        public async Task GivenOnlyHeight_ShouldResizeToFitWidthAndMaintainAspectRatio()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
            var sut = Create(fetcher);
            // source image is 800x600
            // -> if height is specified as 150, implies width of 200
            var expectedWidth = 200;
            var expectedHeight = 150;
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Height = expectedHeight,
                    Url = url,
                },
                NoHeaders
            );
            // Assert
            var image = await Image.LoadAsync(result.Stream);
            Expect(image)
                .To.Have.Width(expectedWidth);
            Expect(image)
                .To.Have.Height(expectedHeight);
        }

        [Test]
        public async Task GivenOnlyHeightAndDevicePixelRatio_ShouldResizeToFitWidthAndMaintainAspectRatio()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
            var sut = Create(fetcher);
            // source image is 800x600
            // -> if height is specified as 150, implies width of 200
            var effectiveHeight = 150;
            var devicePixelRatio = GetRandomDevicePixelRatio();
            var expectedWidth = (int) Math.Ceiling(200 * devicePixelRatio);
            var expectedHeight = (int) Math.Ceiling(effectiveHeight * devicePixelRatio);
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Height = effectiveHeight,
                    DevicePixelRatio = devicePixelRatio,
                    Url = url,
                },
                NoHeaders
            );
            // Assert
            var image = await Image.LoadAsync(result.Stream);
            Expect(image)
                .To.Have.Width(expectedWidth, DumpDebugInfo);
            Expect(image)
                .To.Have.Height(expectedHeight, DumpDebugInfo);

            string DumpDebugInfo()
            {
                return new
                {
                    expectedWidth,
                    expectedHeight,
                    effectiveHeight,
                    devicePixelRatio
                }.Stringify();
            }
        }

        private static decimal GetRandomDevicePixelRatio()
        {
            // truly random decimals are causing obi-wans
            // -> something, somewhere is doing Math.Ceiling
            //    differently
            // -> so rather select a random from a well-behaved table
            return GetRandomFrom(
                new[]
                {
                    1.0M, // regular clients
                    1.325M, // Nexus 7
                    1.5M, // Nexus S
                    2M, // HTC One
                    // there are larger ones, but they would break tests here
                    //  because ResizeMode.Max will not blow images up
                    //  - client should be scaling this anyways, or specifically
                    //    asking for ResizeMode.Stretch
                }
            );
        }
    }

    [TestFixture]
    public class WhenFormatNotSpecified: TestBase
    {
        [Test]
        public async Task ShouldResizePngToPng()
        {
            // Arrange
            var bmp = Resources.Images.FluffyCatBmp;
            var pngMemStream = new MemoryStream();
            await bmp.Clone().SaveAsPngAsync(pngMemStream);
            pngMemStream.Rewind();
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(pngMemStream, url);
            var sut = Create(fetcher);
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Height = 400,
                    Url = url
                },
                NoHeaders
            );

            // Assert
            var format = await Image.DetectFormatAsync(result.Stream);
            // should still be able to load after DetectFormat
            Expect(() => Image.Load(result.Stream))
                .Not.To.Throw();
            Expect(format.Name)
                .To.Equal(PngFormat.Instance.Name);
        }

        [Test]
        public async Task ShouldResizeBmpToJpg()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
            var sut = Create(fetcher);
            var expected = GetRandomInt(100, 200);
            var options = new ImageResizeOptions()
            {
                Url = url,
                Width = expected
            };
            // Act
            var result = await sut.Resize(options, NoHeaders);
            // Assert
            Expect(await Image.DetectFormatAsync(result.Stream)).To.Equal(JpegFormat.Instance);
            Expect(await Image.LoadAsync(result.Stream)).To.Have.Width(expected);
        }

        [Test]
        public async Task ShouldResizeGifToGif()
        {
            // Arrange
            var url = GetRandomHttpUrlWithPath();
            var bmp = Resources.Images.FluffyCatBmp;
            var gifMemStream = new MemoryStream();
            await bmp.Clone().SaveAsGifAsync(gifMemStream);
            var fetcher = CreateFetcherFor(gifMemStream.ToArray(), url);
            var sut = Create(fetcher);
            var expected = GetRandomInt(100, 200);
            var options = new ImageResizeOptions()
            {
                Url = url,
                Width = expected
            };
            // Act
            var result = await sut.Resize(options, NoHeaders);
            // Assert
            Expect(await Image.DetectFormatAsync(result.Stream)).To.Equal(GifFormat.Instance);
            Expect(await Image.LoadAsync(result.Stream)).To.Have.Width(expected);
        }
    }

    [Test]
    public async Task ShouldResizeToBmpWhenExplicitlyRequested()
    {
        // Arrange
        var url = GetRandomHttpUrlWithPath();
        var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
        var expected = GetRandomInt(100, 200);
        var options = new ImageResizeOptions()
        {
            Width = expected,
            Format = "BMP",
            Url = url
        };
        var sut = Create(fetcher);
        // Act
        var result = await sut.Resize(options, NoHeaders);
        // Assert
        Expect(await Image.DetectFormatAsync(result.Stream)).To.Be(BmpFormat.Instance);
        Expect(await Image.LoadAsync(result.Stream)).To.Have.Width(expected);
    }

    [TestFixture]
    public class IntegrationLike
    {
        // some of the logic within the processor relies on accessing a
        // stream twice -- which is fine for MemoryStreams, but may not
        // be fine for streams from a web response. So this test is to verify
        // that an invocation which would rely on auto-formatting can succeed
        [Test]
        public async Task ShouldBeAbleToResizeForReal()
        {
            var appSettings = Substitute.For<IAppSettings>();
            appSettings.MaxInputImageSize.Returns(40 * 1024 * 1024);
            appSettings.MaxOutputImageSize.Returns(40 * 1024 * 1024);
            appSettings.MaxImageFetchTimeInMilliseconds.Returns(10000);
            var bmp = Image.Load(Resources.Data.FluffyCatBmp);
            var pngStream = new MemoryStream();
            await bmp.SaveAsPngAsync(pngStream);
            var png = pngStream.ToArray();
            // Arrange
            using (var server = new HttpServer())
            {
                var servedPath = "/fluffy-cat.png";
                server.ServeFile(servedPath, () => png);
                var fetcher = new UrlFetcher(
                    appSettings,
                    Substitute.For<ILogger<UrlFetcher>>()
                );
                var sut = Create(fetcher);
                var options = new ImageResizeOptions()
                {
                    Url = server.GetFullUrlFor(servedPath),
                    Width = 200
                };
                // Act
                var result = await sut.Resize(options, NoHeaders);
                // Assert
                var format = await Image.DetectFormatAsync(result.Stream);
                Expect(format.Name)
                    .To.Equal(PngFormat.Instance.Name);
                Expect(() => Image.Load(result.Stream))
                    .Not.To.Throw();
            }
        }
    }

    [Test]
    public async Task ShouldUseProvidedResampler()
    {
        // Arrange
        var url = GetRandomHttpUrlWithPath();
        var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
        var opts0 = new ImageResizeOptions()
        {
            Width = 200,
            Url = url
        };
        var opts1 = new ImageResizeOptions()
        {
            Width = 200,
            Sampler = "bicubic",
            Url = url
        };
        var opts2 = new ImageResizeOptions()
        {
            Width = 200,
            Sampler = "box",
            Url = url
        };
        var sut = Create(fetcher);
        // Act
        var result0 = await sut.Resize(opts0, NoHeaders);
        var result1 = await sut.Resize(opts1, NoHeaders);
        var result2 = await sut.Resize(opts2, NoHeaders);
        // Assert
        var bytes0 = await result0.Stream.ReadAllBytesAsync();
        var bytes1 = await result1.Stream.ReadAllBytesAsync();
        var bytes2 = await result2.Stream.ReadAllBytesAsync();
        Expect(bytes0)
            .To.Equal(bytes1, "Should default to bicubic sampler");
        Expect(bytes1)
            .Not.To.Equal(bytes2, "Should use the provided sampler by name");
    }

    [Test]
    public async Task ShouldUseProvidedQuantizer()
    {
        // Arrange
        var url = GetRandomHttpUrlWithPath();
        var fetcher = CreateFetcherFor(Resources.Data.FluffyCatBmp, url);
        var width = 800;
        var opts0 = new ImageResizeOptions()
        {
            Width = width,
            Url = url,
            Format = "PNG"
        };
        var opts1 = new ImageResizeOptions()
        {
            Width = width,
            Quantizer = "wu",
            Url = url,
            Format = "PNG"
        };
        var opts2 = new ImageResizeOptions()
        {
            Width = width,
            Quantizer = "octree",
            MaxColors = 6,
            Url = url,
            Format = "PNG"
        };
        var sut = Create(fetcher);
        // Act
        var result0 = await sut.Resize(opts0, NoHeaders);
        var result1 = await sut.Resize(opts1, NoHeaders);
        var result2 = await sut.Resize(opts2, NoHeaders);
        Expect(await Image.DetectFormatAsync(result0.Stream)).To.Equal(PngFormat.Instance);
        Expect(await Image.DetectFormatAsync(result1.Stream)).To.Equal(PngFormat.Instance);
        Expect(await Image.DetectFormatAsync(result2.Stream)).To.Equal(PngFormat.Instance);

        // Assert
        // Note: quantizers can produce different results on images with an indexed
        // color palette, like gif or png. The default was originally chosen to be
        // wu based on not seeing any noticeable difference between them, but this
        // has been changed to octree after some strange-looking resizes of
        // animated gif images
//            var bytes0 = result0.ReadAllBytes();
//            var bytes1 = result1.ReadAllBytes();
//            var bytes2 = result2.ReadAllBytes();
//            Expect(bytes0).To.Equal(bytes1, "Should default to bicubic sampler");
//            Expect(bytes1).Not.To.Equal(bytes2, "Should use the provided sampler by name");
    }

    [TestFixture]
    public class OperationTimingsHeaders
    {
        [Test]
        public async Task ShouldIncludeTimeToFetchImage()
        {
            // Arrange
            var min = 500;
            var max = 1500;
            var toSleep = GetRandomInt(min, max);
            var fetchResult = new MemoryStream(Resources.Data.FluffyCatBmp);
            var fetcher = CreateFetcherFor(
                () =>
                {
                    Thread.Sleep(toSleep);
                    return fetchResult;
                }
            );
            var sut = Create(fetcher);
            // Act
            var result = await sut.Resize(
                new ImageResizeOptions()
                {
                    Width = Resources.Images.FluffyCatBmp.Width,
                    Url = GetRandomHttpUrlWithPath(),
                    Format = "PNG"
                },
                new Dictionary<string, string>()
            );
            // Assert
            Expect(result.Headers)
                .To.Contain.Key(TimingHeaders.Fetch);
            var header = result.Headers[TimingHeaders.Fetch];
            Expect(header)
                .Not.To.Be.Null();
            Expect(header)
                .To.Be.An.Integer();
            var intValue = int.Parse(header);
            Expect(intValue)
                .To.Be.Greater.Than.Or.Equal.To(min)
                .And.Less.Than(max + 100); // allow some time for recording
        }

        public static IEnumerable<string> EnumerateTimingHeaders()
        {
            return typeof(TimingHeaders)
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(f => f.Name != nameof(TimingHeaders.PREFIX))
                .Select(f => f.GetValue(null) as string)
                .ToArray();
        }

        [TestCaseSource(nameof(EnumerateTimingHeaders))]
        [Test]
        public async Task ShouldIncludeTimingHeader_(string header)
        {
            // Arrange
            var fetcher = CreateFetcherFor(
                Resources.Data.FluffyCatBmp
            );
            var sut = Create(fetcher);
            var options = new ImageResizeOptions()
            {
                Width = Resources.Images.FluffyCatBmp.Width,
                Url = GetRandomHttpUrlWithPath(),
                Format = "PNG"
            };
            Expect(options.Url)
                .Not.To.Be.Null.Or.Empty($"got: {options.Url}");
            // Act
            var result = await sut.Resize(options, new Dictionary<string, string>());
            // Assert
            Expect(result.Headers)
                .To.Contain.Key(header);
        }
    }

    [Test]
    public void ShouldThrowIfOutputWillExceedMaxOutputSize()
    {
        // Arrange
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.MaxOutputImageSize.Returns(1024);
        appSettings.MaxImageFetchTimeInMilliseconds.Returns(10000);
        var url = GetRandomHttpUrlWithPath();
        var fetcher = CreateFetcherFor(Resources.Streams.FluffyCatBmp, url);
        var sut = Create(fetcher, appSettings);
        var options = new ImageResizeOptions()
        {
            Url = url
        };
        // Act
        Expect(() => sut.Resize(options, NoHeaders))
            .To.Throw<NotSupportedException>();
        // Assert
    }

    private static IUrlFetcher CreateFetcherFor(
        byte[] data,
        string url = null,
        IDictionary<string, string> headers = null
    )
    {
        return CreateFetcherFor(
            () => new MemoryStream(data),
            url,
            headers
        );
    }

    private static IUrlFetcher CreateFetcherFor(
        Stream data,
        string url = null,
        IDictionary<string, string> headers = null
    )
    {
        return CreateFetcherFor(() => data, url, headers);
    }

    private static IUrlFetcher CreateFetcherFor(
        Func<Stream> dataProvider,
        string url = null,
        IDictionary<string, string> responseHeaders = null
    )
    {
        var result = Substitute.For<IUrlFetcher>();
        result.Fetch(url ?? Arg.Any<string>(), Arg.Any<IDictionary<string, string>>())
            .ReturnsForAnyArgs(
                ci => new StreamAndHeaders(
                    dataProvider(),
                    responseHeaders ?? new Dictionary<string, string>()
                )
            );
        return result;
    }

    private static ImageResizer Create(
        IUrlFetcher fetcher,
        IAppSettings appSettings = null
    )
    {
        return new ImageResizer(
            fetcher,
            appSettings ?? CreateDefaultAppSettings()
        );
    }

    private static IAppSettings CreateDefaultAppSettings()
    {
        var result = Substitute.For<IAppSettings>();
        var _40mb = 40 * 1024 * 1024;
        result.MaxInputImageSize.Returns(_40mb);
        result.MaxOutputImageSize.Returns(_40mb);
        result.MaxImageFetchTimeInMilliseconds.Returns(10000);
        return result;
    }
}

public static class DictionaryExtensions
{
    public static bool IsEquivalentTo<T1, T2>(
        this IDictionary<T1, T2> src,
        IDictionary<T1, T2> other
    )
    {
        // TODO: replace with PB.DeepEquals when that's fixed for netstandard
        var srcKeys = src.Keys.ToArray();
        var otherKeys = other.Keys.ToArray();
        if (srcKeys.Length != otherKeys.Length)
        {
            return false;
        }

        var keysMatch = srcKeys.All(sk => otherKeys.Contains(sk));
        if (!keysMatch)
        {
            return false;
        }

        return srcKeys.Aggregate(
            true,
            (acc, cur) => acc && src[cur].Equals(other[cur])
        );
    }
}

public static class Matchers
{
    public static IMore<string> Integer(
        this IAn<string> an
    )
    {
        return an.Compose(
            actual =>
                Expect(int.TryParse(actual, out var _))
                    .To.Be.True($"Could not parse '{actual}' as an integer")
        );
    }
}