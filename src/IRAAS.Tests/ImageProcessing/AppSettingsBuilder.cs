using IRAAS.ImageProcessing;
using PeanutButter.RandomGenerators;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using static PeanutButter.RandomGenerators.RandomValueGen;

namespace IRAAS.Tests.ImageProcessing;

// ReSharper disable once UnusedType.Global
public class AppSettingsBuilder
    : GenericBuilder<AppSettingsBuilder, AppSettings>
{
    public override AppSettingsBuilder WithRandomProps()
    {
        return base.WithRandomProps()
            .WithNonZeroConcurrency()
            .WithValidMaxImageFetchTime();
    }

    public AppSettingsBuilder WithValidMaxImageFetchTime()
    {
        return WithProp(
            o => o.MaxImageFetchTimeInMilliseconds = GetRandomInt(1000, 2000)
        );
    }

    public AppSettingsBuilder WithNonZeroConcurrency()
    {
        return WithProp(o => o.MaxConcurrency = GetRandomInt(1));
    }
}

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
[RequireNonZero(nameof(IImageResizeParameters.Width))]
// TODO: re-enable after PB is upgraded to allow multiple [RequireNonZero] decorations
// [RequireNonZero(nameof(IImageResizeParameters.Height))]
public class DefaultImageResizeParametersBuilder
    : GenericBuilder<DefaultImageResizeParametersBuilder, DefaultImageResizeParameters>
{
}