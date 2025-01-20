using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using PeanutButter.Utils;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Pbm;
using SixLabors.ImageSharp.Formats.Qoi;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;

namespace IRAAS.ImageProcessing;

public class ImageResizeOptions
    : IImageResizeParameters
{
    public static void ClearDefaults()
    {
        SetDefaults(null);
    }

    public static void SetDefaults(
        IImageResizeParameters parameters
    )
    {
        _defaultParameters = parameters;
    }

    public static IImageResizeParameters Defaults => _defaultParameters;

    public ImageResizeOptions()
    {
        if (_defaultParameters is null)
        {
            return;
        }

        _defaultParameters.CopyPropertiesTo(this);
    }

    private static IImageResizeParameters _defaultParameters;

    public const int DEFAULT_QUALITY = 85;

    public string Url
    {
        get => _url;
        set
        {
            try
            {
                var uri = new Uri(value);
                _url = uri.HasPath() || uri.HasParameters()
                    ? uri.ToString()
                    : null;
            }
            catch
            {
                _url = null;
            }
        }
    }


    private string _url;

    public const string PNG_FORMAT_SPECIFIER = "png";

    public bool OutputFormatSpecified =>
        !string.IsNullOrWhiteSpace(Format);

    public bool PngOutputRequested =>
        string.Equals(Format, PNG_FORMAT_SPECIFIER, StringComparison.OrdinalIgnoreCase);

    public string ReplaceTransparencyWith { get; set; }

    [Options("", "jpeg", "png", "gif", "bmp")]
    public string Format
    {
        get => _format;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _format = null;
                return;
            }

            _format = FormatLookup.TryGetValue(value, out var parsed)
                ? parsed
                : JpegFormat.Instance.Name;
        }
    }

    [Min(0)]
    [Max(100)]
    public int Quality
    {
        get => _quality is null or < 1
            ? DEFAULT_QUALITY
            : _quality.Value;
        set
        {
            if (value < 1 || value > 100)
            {
                _quality = null;
                return;
            }

            _quality = value;
        }
    }


    [Min(0)]
    [Max(2048)]
    [Default(400)]
    public int? Width
    {
        get => _width;
        set => _width = (value ?? 0) > 0
            ? value
            : null;
    }

    private int? _width;

    [Min(0)]
    [Max(2048)]
    [Default(300)]
    public int? Height
    {
        get => _height;
        set => _height = (value ?? 0) > 0
            ? value
            : null;
    }

    private int? _height;

    public ResizeMode? ResizeMode { get; set; }

    [Obsolete("JpegColorType was renamed upstream to JpegEncodingColor")]
    public JpegEncodingColor? JpegColorType
    {
        get => JpegEncodingColor;
        set => JpegEncodingColor = value;
    }

    public JpegEncodingColor? JpegEncodingColor { get; set; }

    [Min(0)]
    [Max(1)]
    [Step(0.1)]
    public float? Gamma { get; set; }

    [OptionsFrom(typeof(ImageResizer), nameof(ImageResizer.QuantizerNames))]
    public string Quantizer { get; set; }

    public byte? TransparencyThreshold { get; set; }

    [Options(
        "",
        "1",
        "2",
        "4",
        "8",
        "16",
        "24",
        "32"
    )]
    public int? BitDepth { get; set; }

    public PngColorType? PngColorType { get; set; }

    [Min(1)]
    [Max(9)]
    public int? CompressionLevel { get; set; }

    public PngFilterMethod? PngFilterMethod { get; set; }

    [OptionsFrom(typeof(ImageResizer), nameof(ImageResizer.SamplerNames))]
    public string Sampler { get; set; }

    public GifColorTableMode? GifColorTableMode { get; set; }

    [Min(0)]
    public int? MaxColors { get; set; }

    [Options("", "true", "false")]
    public bool? Dither { get; set; }

    [Min(1)]
    [Max(4)]
    [Step(0.1)]
    public decimal DevicePixelRatio
    {
        get => _devicePixelRatio ?? 1;
        set => _devicePixelRatio = value >= 1
            ? value
            : 1;
    }

    [JsonIgnore]
    public int? EffectiveWidth => Width.HasValue
        ? (int?) Math.Ceiling(Width.Value * DevicePixelRatio)
        : null;

    [JsonIgnore]
    public int? EffectiveHeight => Height.HasValue
        ? (int?) Math.Ceiling(Height.Value * DevicePixelRatio)
        : null;


    private decimal? _devicePixelRatio;


    private int? _quality;

    private static readonly Dictionary<string, string> FormatLookup
        = GenerateFormatLookup();

    private static Dictionary<string, string> GenerateFormatLookup()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        new[]
        {
            BmpFormat.Instance.Name,
            JpegFormat.Instance.Name,
            GifFormat.Instance.Name,
            PngFormat.Instance.Name,
            WebpFormat.Instance.Name
        }.ForEach(
            format =>
            {
                result[format] = format;
            }
        );
        result["jpg"] = JpegFormat.Instance.Name;
        return result;
    }

    private string _format;

    public void DetermineOutputFormatIfNotSpecified(
        IImageFormat sourceFormat
    )
    {
        if (Format is not null)
        {
            return;
        }

        Format = sourceFormat.Equals(BmpFormat.Instance)
            ? JpegFormat.Instance.Name
            : sourceFormat.Name;
        if (AutoConversionTable.TryGetValue(sourceFormat.Name, out var convertTo))
        {
            Format = convertTo;
        }
    }

    private static readonly Dictionary<string, string> AutoConversionTable = new(StringComparer.OrdinalIgnoreCase)
    {
        [BmpFormat.Instance.Name] = JpegFormat.Instance.Name,
        [QoiFormat.Instance.Name] = JpegFormat.Instance.Name,
        // unoptimised image format which supports frames
        // - I've seen icons with this format
        [PbmFormat.Instance.Name] = PngFormat.Instance.Name,
        // commonly used in game assets, this is an old format
        // which often has natural images in it
        [TgaFormat.Instance.Name] = JpegFormat.Instance.Name,
        // format used by, eg, scanners - provides for
        // multiple pages within the format, but not typically
        // animated
        [TiffFormat.Instance.Name] = PngFormat.Instance.Name
    };
}