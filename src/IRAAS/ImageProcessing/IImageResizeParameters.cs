using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace IRAAS.ImageProcessing;

public interface IImageResizeParameters
{
    string ReplaceTransparencyWith { get; set; }
    string Format { get; set; }
    [Default(85)]
    int Quality { get; set; }
    int? Width { get; set; }
    int? Height { get; set; }
    ResizeMode? ResizeMode { get; set; }
    JpegEncodingColor? JpegColorType { get; set; }
    JpegEncodingColor? JpegEncodingColor { get; set; }
    float? Gamma { get; set; }
    string Quantizer { get; set; }
    byte? TransparencyThreshold { get; set; }
    int? BitDepth { get; set; }
    PngColorType? PngColorType { get; set; }
    int? CompressionLevel { get; set; }
    PngFilterMethod? PngFilterMethod { get; set; }
    string Sampler { get; set; }
    GifColorTableMode? GifColorTableMode { get; set; }
    int? MaxColors { get; set; }
    bool? Dither { get; set; }
    decimal DevicePixelRatio { get; set; }
}