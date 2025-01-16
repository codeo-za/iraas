using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace IRAAS.ImageProcessing;

public interface IImageMimeTypeProvider
{
    string DetermineMimeTypeFor(Stream imageStream);
}

public class ImageMimeTypeProvider : IImageMimeTypeProvider
{
    public string DetermineMimeTypeFor(Stream imageStream)
    {
        if (imageStream is null)
        {
            throw new ArgumentNullException(nameof(imageStream));
        }

        IImageFormat format = null;
        try
        {
            format = Image.DetectFormat(imageStream);
        }
        catch (UnknownImageFormatException)
        {
            // suppress - DetectFormat used to return
            // null, now it throws on error
        }

        if (format is null)
        {
            throw new NotSupportedException(
                "Unsupported image stream"
            );
        }

        return MimeTypeLookup.TryGetValue(format, out var result)
            ? result
            : throw new NotSupportedException($"Image type {format.Name} is not supported");
    }

    private static readonly Dictionary<IImageFormat, string> MimeTypeLookup =
        new()
        {
            [BmpFormat.Instance] = "image/bmp",
            [JpegFormat.Instance] = "image/jpeg",
            [GifFormat.Instance] = "image/gif",
            [PngFormat.Instance] = "image/png",
            [WebpFormat.Instance] = "image/webp"
        };
}