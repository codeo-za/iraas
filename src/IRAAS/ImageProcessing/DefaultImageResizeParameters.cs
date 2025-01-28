using System;
using System.Collections.Generic;
using PeanutButter.DuckTyping.Extensions;
using PeanutButter.Utils;
using PeanutButter.Utils.Dictionaries;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace IRAAS.ImageProcessing;

public interface IDefaultImageResizeParameters : IImageResizeParameters
{
    IImageResizeParameters For(string imageFormat);

    void RegisterPerFormatDefaultsFor(
        string format,
        IDictionary<string, string> overrides
    );
}

public class DefaultImageResizeParameters : IDefaultImageResizeParameters
{
    public string ReplaceTransparencyWith { get; set; }
    public string Format { get; set; }
    public int Quality { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public ResizeMode? ResizeMode { get; set; } = SixLabors.ImageSharp.Processing.ResizeMode.Crop;
    public JpegEncodingColor? JpegColorType { get; set; }
    public JpegEncodingColor? JpegEncodingColor { get; set; }
    public float? Gamma { get; set; }
    public string Quantizer { get; set; } = "Wu";
    public byte? TransparencyThreshold { get; set; }
    public int? BitDepth { get; set; } = 24;
    public PngColorType? PngColorType { get; set; }
    public int? CompressionLevel { get; set; }
    public PngFilterMethod? PngFilterMethod { get; set; }
    public string Sampler { get; set; }
    public GifColorTableMode? GifColorTableMode { get; set; }
    public int? MaxColors { get; set; }
    public bool? Dither { get; set; }
    public decimal DevicePixelRatio { get; set; } = 1;

    public static DefaultImageResizeParameters From(
        IDictionary<string, string> rawConfig
    )
    {
        ArgumentNullException.ThrowIfNull(rawConfig, nameof(rawConfig));
        var ducked = rawConfig.FuzzyDuckAs<IImageResizeParameters>(
            throwOnError: true
        );
        var result = new DefaultImageResizeParameters();
        ducked.CopyPropertiesTo(result);
        var target = result._rawDefaultParameters;
        target.Clear();
        foreach (var kvp in rawConfig)
        {
            target[kvp.Key] = kvp.Value;
        }

        return result;
    }

    private readonly Dictionary<string, string> _rawDefaultParameters = new();
    private readonly Dictionary<string, IImageResizeParameters> _perFormatParameters = new();

    public IImageResizeParameters For(string imageFormat)
    {
        var result = _perFormatParameters.TryGetValue(imageFormat, out var perFormat)
            ? perFormat
            : this;
        return Sanitise(result);
    }

    /// <summary>
    /// A last-ditch attempt to provide hard-coded defaults
    /// where none are found
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    public static IImageResizeParameters Sanitise(
        IImageResizeParameters result
    )
    {
        // TODO: move the logic of the "create default" items from
        // image resizer here
        if (result.Quality < 1)
        {
            result.Quality = 85;
        }

        if (string.IsNullOrWhiteSpace(result.Sampler))
        {
            result.Sampler = "Bicubic";
        }

        if (result.Quantizer is null)
        {
            result.Quantizer = "Wu";
        }

        return result;
    }

    public void RegisterPerFormatDefaultsFor(
        string format,
        IDictionary<string, string> overrides
    )
    {
        var merged = new MergeDictionary<string, string>(
            overrides,
            _rawDefaultParameters
        );
        _perFormatParameters[format] = From(merged);
    }
}