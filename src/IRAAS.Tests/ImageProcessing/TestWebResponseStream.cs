using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using IRAAS.ImageProcessing;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.Utils;

namespace IRAAS.Tests.ImageProcessing;

[TestFixture]
public class TestWebResponseStream
{
    [Test]
    public async Task ShouldBeAbleToReadEntireStreamFromResponse()
    {
        // Arrange
        using var server = HttpServerPool.Borrow();
        var path = $"/{GetRandomString()}";
        server.ServeFile(path, () => Resources.Data.FluffyCatBmp);
        // Act
        var sut = await Create(server.GetFullUrlFor(path));
        // Assert
        var result = await sut.ReadAllBytesAsync();
        Expect(result)
            .To.Equal(Resources.Data.FluffyCatBmp);
    }

    [Test]
    [Repeat(100)]
    public async Task ShouldBeAbleToReadABitAndRewindAndReadAll()
    {
        // Arrange
        var bufferSize = 100;
        using var server = HttpServerPool.Borrow();
        var path = $"/{GetRandomString()}";
        server.ServeFile(path, () => Resources.Data.FluffyCatBmp);
        // Act
        var sut = await Create(server.GetFullUrlFor(path));
        // Assert
        Expect(sut.CanSeek)
            .To.Be.True("Should be able to seek");
        Expect(sut.CanWrite)
            .To.Be.False("Should not be able to write");
        Expect(sut.CanRead)
            .To.Be.True("Should be able to read");
        var buffer = new byte[bufferSize];
        var read = sut.Read(buffer, 0, bufferSize);

        Expect(read)
            .To.Equal(bufferSize);
        Expect(buffer).To.Equal(
            Resources.Data.FluffyCatBmp.Take(bufferSize)
        );

        sut.Rewind();
        var result = new byte[Resources.Data.FluffyCatBmp.Length];
        var secondRead = 0;
        var thisRead = 0;
        do
        {
            thisRead = sut.Read(result, secondRead, result.Length - secondRead);
            secondRead += thisRead;
        } while (thisRead > 0);

        Expect(secondRead)
            .To.Equal(result.Length);
        Expect(result)
            .To.Equal(Resources.Data.FluffyCatBmp);
    }

    [Test]
    public async Task ShouldBeAbleToQueryLength()
    {
        // Arrange
        using var server = HttpServerPool.Borrow();
        var path = $"/{GetRandomString()}";
        server.ServeFile(path, () => Resources.Data.FluffyCatBmp);
        // Act
        var sut = await Create(server.GetFullUrlFor(path));
        // Assert
        var result = sut.Length;
        Expect(result)
            .To.Equal(Resources.Data.FluffyCatBmp.Length);
    }

    [Test]
    public async Task ShouldBeAbleToQueryLengthAndThenRewindAndReadAll()
    {
        // Arrange
        using var server = HttpServerPool.Borrow();
        var path = $"/{GetRandomString()}";
        server.ServeFile(path, () => Resources.Data.FluffyCatBmp);
        // Act
        var sut = await Create(server.GetFullUrlFor(path));
        // Assert
        var result = sut.Length;
        Expect(result)
            .To.Equal(Resources.Data.FluffyCatBmp.Length);
        sut.Rewind();
        var allData = await sut.ReadAllBytesAsync();
        Expect(allData)
            .To.Equal(Resources.Data.FluffyCatBmp);
    }

    [Test]
    public async Task ShouldThrowWhenInputImageIsTooLarge()
    {
        // Arrange
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.MaxImageFetchTimeInMilliseconds.Returns(10000);
        appSettings.MaxInputImageSize.Returns(Resources.Data.FluffyCatBmp.Length - 1);
        appSettings.MaxOutputImageSize.Returns(int.MaxValue);
        var path = $"/{GetRandomString()}";
        using var server = HttpServerPool.Borrow();
        server.ServeFile(path, () => Resources.Data.FluffyCatBmp);
        // Act
        var sut = await Create(server.GetFullUrlFor(path), appSettings);
        // Assert
        Expect(() => sut.Length)
            .To.Throw<NotSupportedException>();
    }

    private static async Task<WebResponseStream> Create(
        string url,
        IAppSettings appSettings = null)
    {
        var httpClient = new HttpClient();
        var stream = await httpClient.GetStreamAsync(url);
        return new WebResponseStream(
            stream,
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
