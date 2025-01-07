using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using PeanutButter.SimpleHTTPServer;
using PeanutButter.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using SystemImage = System.Drawing.Image;

namespace IRAAS.Tests;

[TestFixture]
[Explicit("Discovery tests")]
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class DiscoveryTests: TestBase
{
    [Test]
    public async Task ResizingAnImageFromUrl()
    {
        // Arrange
        var httpClient = new HttpClient();
        using var server = new HttpServer();
        using var target = new AutoTempFile();
        await using var targetStream = File.Open(target.Path, FileMode.OpenOrCreate);
        var path = "/fluffy-cat.bmp";
        server.ServeFile(path, () => Resources.Data.FluffyCatBmp);
        var url = server.GetFullUrlFor(path);
        var width = 640;
        var height = 480;
        var sourceStream = await httpClient.GetStreamAsync(url);
        // Act
        var source = Image.Load(sourceStream);
        var clone = source.Clone(ctx => ctx.Resize(
            new ResizeOptions()
            {
                Mode = ResizeMode.Max,
                Size = new Size(width, height),
                Compand = true,
                Sampler = new BicubicResampler()
            }));
        await clone.SaveAsJpegAsync(
            targetStream,
            new JpegEncoder()
            {
                Quality = 85,
                ColorType = JpegEncodingColor.YCbCrRatio420
            });
        targetStream.Close();
        // Assert
        var format = Image.DetectFormat(target.BinaryData);
        Expect(format.Name)
            .To.Equal(JpegFormat.Instance.Name);

        var result = Image.Load(target.BinaryData);
        Expect(result.Metadata.GetFormatMetadata(JpegFormat.Instance).Quality)
            .To.Equal(85);
        Expect(target.BinaryData.Length)
            .To.Be.Less.Than(Resources.Data.FluffyCatBmp.Length);
        var sysImage = SystemImage.FromStream(new MemoryStream(target.BinaryData));
        Expect(sysImage.RawFormat)
            .To.Equal(ImageFormat.Jpeg);
    }

    [Test]
    public void WhatDoesImageSharpDoWithGarbage()
    {
        // Arrange
        var stream = new MemoryStream(GetRandomBytes(1024, 2048));
        // Act
        Expect(() => Image.Load(stream))
            .To.Throw<UnknownImageFormatException>();
        // Assert
    }

    [Test]
    public async Task DisposingOfWebResponseBeforeStream()
    {
        // Arrange
        Stream sourceStream;
        var httpClient = new HttpClient();
        using var server = new HttpServer();
        using var target = new AutoTempFile();
        await using var targetStream = File.Open(target.Path, FileMode.OpenOrCreate);
        var path = "/fluffy-cat.bmp";
        server.ServeFile(path, () => Resources.Data.FluffyCatBmp);
        var url = server.GetFullUrlFor(path);
        var req = await httpClient.GetAsync(url);
        await using (var stream = await req.Content.ReadAsStreamAsync())
        {
            sourceStream = stream;
        }

        // Act
        GC.Collect();
        GC.WaitForPendingFinalizers();
        // Image.Load doesn't get a seekable stream, so
        //    throws the same exception as if the incoming
        //    image format were unsupported, which, technically,
        //    it probably is
        Expect(() => Image.Load(sourceStream))
            .To.Throw<NotSupportedException>();
    }
}