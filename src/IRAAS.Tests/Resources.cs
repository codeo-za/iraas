using System;
using System.Collections.Concurrent;
using System.IO;
using PeanutButter.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace IRAAS.Tests;

public static class Resources
{
    public static class Paths
    {
        public const string FLUFFY_CAT_BMP = "resources/fluffy-cat.bmp";
        public const string FLUFFY_CAT_JPEG = "resources/fluffy-cat.jpg";
    }

    public static class Data
    {
        public static byte[] FluffyCatBmp => GetResource(Paths.FLUFFY_CAT_BMP);
        public static byte[] FluffyCatJpeg => GetResource(Paths.FLUFFY_CAT_JPEG);
    }

    public static class Streams
    {
        public static Stream FluffyCatBmp => GetStream(Paths.FLUFFY_CAT_BMP);
        public static Stream FluffyCatJpeg => GetStream(Paths.FLUFFY_CAT_JPEG);

        public static Stream FluffyCatPng = GetStream(
            "::png::",
            CreateFluffyCatPngStream);

        public static Stream FluffyCatGif = GetStream("::gif::", CreateFluffyCatGifStream);
    }

    public static class Images
    {
        public static Image<Rgba32> FluffyCatBmp => GetImage(Paths.FLUFFY_CAT_BMP);
        public static Image<Rgba32> FluffyCatJpeg => GetImage(Paths.FLUFFY_CAT_JPEG);
    }


    private static readonly ConcurrentDictionary<string, byte[]> ResourceData
        = new ConcurrentDictionary<string, byte[]>();

    private static byte[] GetResource(string path)
    {
        if (ResourceData.TryGetValue(path, out var data))
        {
            return data;
        }

        return ResourceData[path] = File.ReadAllBytes(path);
    }

    private static readonly ConcurrentDictionary<string, Image<Rgba32>> ResourceImages
        = new ConcurrentDictionary<string, Image<Rgba32>>();

    private static Image<Rgba32> GetImage(string path)
    {
        if (ResourceImages.TryGetValue(path, out var result))
        {
            return result;
        }

        var data = GetResource(path);
        return ResourceImages[path] = Image.Load<Rgba32>(data);
    }

    private static readonly ConcurrentDictionary<string, byte[]> CachedData
        = new ConcurrentDictionary<string, byte[]>();

    private static Stream GetStream(
        string path,
        Func<byte[]> providedData = null)
    {
        if (CachedData.TryGetValue(path, out var result))
        {
            return new MemoryStream(result);
        }

        var data = providedData?.Invoke() ?? GetResource(path);
        CachedData[path] = data;
        return new MemoryStream(data);
    }

    private static byte[] CreateFluffyCatPngStream()
    {
        var result = new MemoryStream();
        Images.FluffyCatBmp.Clone()
            .SaveAsPng(result);
        result.Rewind();
        return result.ToArray();
    }

    private static byte[] CreateFluffyCatGifStream()
    {
        var result = new MemoryStream();
        Images.FluffyCatBmp.Clone()
            .SaveAsGif(result);
        result.Rewind();
        return result.ToArray();
    }
}