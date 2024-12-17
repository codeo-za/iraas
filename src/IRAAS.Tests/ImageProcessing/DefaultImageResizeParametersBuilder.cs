using IRAAS.ImageProcessing;
using PeanutButter.RandomGenerators;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

// ReSharper disable UnusedType.Global

namespace IRAAS.Tests.ImageProcessing;

[Restrict(nameof(IImageResizeParameters.Format), "JPEG", "PNG", "GIF", "BMP")]
[Restrict(
    nameof(IImageResizeParameters.ResizeMode),
    ResizeMode.Crop,
    ResizeMode.Pad,
    ResizeMode.BoxPad,
    ResizeMode.Max,
    ResizeMode.Min,
    ResizeMode.Stretch
)]
[Restrict(
    nameof(IImageResizeParameters.JpegEncodingColor),
    JpegEncodingColor.YCbCrRatio420,
    JpegEncodingColor.YCbCrRatio444,
    JpegEncodingColor.YCbCrRatio444,
    JpegEncodingColor.YCbCrRatio422,
    JpegEncodingColor.YCbCrRatio411,
    JpegEncodingColor.YCbCrRatio410,
    JpegEncodingColor.Luminance,
    JpegEncodingColor.Rgb,
    JpegEncodingColor.Cmyk,
    JpegEncodingColor.Ycck
)]
[Restrict(
    nameof(IImageResizeParameters.JpegColorType),
    JpegEncodingColor.YCbCrRatio420,
    JpegEncodingColor.YCbCrRatio444,
    JpegEncodingColor.YCbCrRatio422,
    JpegEncodingColor.YCbCrRatio411,
    JpegEncodingColor.YCbCrRatio410,
    JpegEncodingColor.Luminance,
    JpegEncodingColor.Rgb,
    JpegEncodingColor.Cmyk,
    JpegEncodingColor.Ycck
)]
[Restrict(
    nameof(IImageResizeParameters.PngColorType),
    PngColorType.Grayscale,
    PngColorType.Rgb,
    PngColorType.Palette,
    PngColorType.GrayscaleWithAlpha,
    PngColorType.RgbWithAlpha
)]
[Restrict(
    nameof(IImageResizeParameters.PngFilterMethod),
    PngFilterMethod.None,
    PngFilterMethod.Sub,
    PngFilterMethod.Up,
    PngFilterMethod.Average,
    PngFilterMethod.Paeth,
    PngFilterMethod.Adaptive
)]
[Restrict(
    nameof(IImageResizeParameters.GifColorTableMode),
    GifColorTableMode.Global,
    GifColorTableMode.Local
)]
[RequireNonZero(nameof(IImageResizeParameters.Width))]
[RequireNonZero(nameof(IImageResizeParameters.Height))]
[RequireNonZero(nameof(IImageResizeParameters.Quality))]
public class DefaultImageResizeParametersBuilder
    : GenericBuilder<DefaultImageResizeParametersBuilder, DefaultImageResizeParameters>
{
    public override DefaultImageResizeParameters Build()
    {
        return base.Build();
    }
}