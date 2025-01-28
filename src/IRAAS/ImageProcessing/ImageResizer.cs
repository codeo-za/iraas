using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IRAAS.Exceptions;
using IRAAS.Security;
using PeanutButter.Utils;
using PeanutButter.Utils.Dictionaries;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

// ReSharper disable AccessToDisposedClosure

namespace IRAAS.ImageProcessing;

public interface IImageResizer
{
    Task<StreamAndHeaders> Resize(
        ImageResizeOptions options,
        IDictionary<string, string> requestHeaders
    );
}

public class ImageResizer : IImageResizer
{
    private readonly IUrlFetcher _fetcher;
    private readonly IAppSettings _appSettings;

    public ImageResizer(
        IUrlFetcher fetcher,
        IAppSettings appSettings
    )
    {
        _fetcher = fetcher;
        _appSettings = appSettings;
    }

    public async Task<StreamAndHeaders> Resize(
        ImageResizeOptions options,
        IDictionary<string, string> requestHeaders
    )
    {
        if (string.IsNullOrWhiteSpace(options.Url))
        {
            throw new InvalidProcessingOptionsException($"Url is required (received: {options.Url})");
        }

        var timer = new Timer();
        using var src = await timer.Time(
            TimingHeaders.Fetch,
            () => _fetcher.Fetch(
                options.Url,
                requestHeaders
            )
        );
        IImageFormat sourceFormat = null;
        try
        {
            sourceFormat = timer.Time(
                TimingHeaders.SourceFormatDetection,
                () => Image.DetectFormat(src.Stream)
            );
        }
        catch (UnknownImageFormatException)
        {
            // suppress - previously, ImageSharp would
            // simply return null from DetectFormat
        }

        if (sourceFormat is null)
        {
            throw new NotSupportedException(
                $"Data source at {options.Url} is not a supported image format"
            );
        }
        
        options.ApplyDefaultsFor(sourceFormat.Name);

        timer.Time(
            TimingHeaders.OutputAutoFormatDetection,
            () => options.DetermineOutputFormatIfNotSpecified(sourceFormat)
        );

        if (!_reEncoders.TryGetValue(options.Format, out var reEncoder))
        {
            throw new NotSupportedException($"Output format {options.Format} is not supported");
        }

        var source = timer.Time(
            TimingHeaders.LoadSource,
            () => Image.Load(src.Stream)
        );


        if (sourceFormat is PngFormat &&
            options.OutputFormatSpecified &&
            options.ReplaceTransparencyWith is not null &&
            !options.PngOutputRequested
           )
        {
            // when converting from png, we have to choose what to do with transparency
            // -> ImageSharp's default behavior is to paint black pixels.
            var replacer = options.ReplaceTransparencyWith.AsRgba32();
            source.Mutate(o => o.BackgroundColor(replacer));
        }

        var targetStream = new LimitedMemoryStream(_appSettings.MaxOutputImageSize);

        var clone = timer.Time(
            TimingHeaders.Resize,
            () => source.Clone(
                ctx => ctx.Resize(
                    new ResizeOptions()
                    {
                        Mode = options.ResizeMode ?? ResizeMode.Max,
                        Size = new Size(
                            options.EffectiveWidth ?? source.Width,
                            options.EffectiveHeight ?? source.Height
                        ),
                        Compand = true, // allow pixel color compression / expansion
                        Sampler = DetermineSamplerFor(options)
                    }
                )
            )
        );

        timer.Time(
            TimingHeaders.EncodeOutput,
            () => reEncoder(clone, targetStream, options)
        );
        targetStream.Rewind();
        return new StreamAndHeaders(
            targetStream,
            new MergeDictionary<string, string>(
                src.Headers,
                timer.Timings
            )
        );
    }

    private IResampler DetermineSamplerFor(ImageResizeOptions options)
    {
        if (options.Sampler is null)
        {
            return CreateDefaultResampler();
        }

        return Resamplers.TryGetValue(options.Sampler, out var generator)
            ? generator(options)
            : CreateDefaultResampler();
    }

    private static readonly Dictionary<string, Func<ImageResizeOptions, IResampler>> Resamplers
        = GenerateResamplerLookup();

    public static string[] SamplerNames { get; } = Resamplers.Keys.ToArray();

    private static Dictionary<string, Func<ImageResizeOptions, IResampler>> GenerateResamplerLookup()
    {
        return GenerateLookupFor<IResampler>(
            type => type.Name.RegexReplace("Resampler$", "")
        );
    }

    private static IQuantizer DetermineQuantizerFor(ImageResizeOptions options)
    {
        if (options.Quantizer is null)
        {
            return CreateDefaultQuantizer();
        }

        var result = Quantizers.TryGetValue(options.Quantizer, out var generator)
            ? generator(options)
            : CreateDefaultQuantizer();
        return result;
    }

    private static readonly Dictionary<string, Func<ImageResizeOptions, IQuantizer>> Quantizers
        = GenerateQuantizerLookup();

    public static string[] QuantizerNames { get; } = Quantizers.Keys.ToArray();

    private static Dictionary<string, Func<ImageResizeOptions, IQuantizer>> GenerateQuantizerLookup()
    {
        return GenerateLookupFor(
            type => type.Name.RegexReplace("Quantizer$", ""),
            (type, opts) => QuantizerFactories.TryGetValue(type, out var factory)
                ? factory(opts)
                : Activator.CreateInstance(type) as IQuantizer,
            t => t != typeof(PaletteQuantizer)
        );
    }

    private static readonly Dictionary<Type, Func<ImageResizeOptions, IQuantizer>> QuantizerFactories
        = new()
        {
            [typeof(WuQuantizer)] = CreateWuQuantizer,
            [typeof(WernerPaletteQuantizer)] = CreateWernerPaletteQuantizer,
            [typeof(OctreeQuantizer)] = CreateOctreeQuantizer,
            [typeof(WebSafePaletteQuantizer)] = CreateWebSafePaletteQuantizer
        };

    private static IQuantizer CreateWebSafePaletteQuantizer(ImageResizeOptions arg)
    {
        return new WebSafePaletteQuantizer(arg.AsQuantizerOptions());
    }

    private static IQuantizer CreateOctreeQuantizer(ImageResizeOptions arg)
    {
        return new OctreeQuantizer(arg.AsQuantizerOptions());
    }

    private static IQuantizer CreateWernerPaletteQuantizer(ImageResizeOptions arg)
    {
        return new WernerPaletteQuantizer(arg.AsQuantizerOptions());
    }

    private static IQuantizer CreateWuQuantizer(ImageResizeOptions arg)
    {
        return new WuQuantizer(arg.AsQuantizerOptions());
    }

    private static Dictionary<string, Func<ImageResizeOptions, T>> GenerateLookupFor<T>(
        Func<Type, string> keyGenerator,
        Func<Type, ImageResizeOptions, T> generator = null,
        Func<Type, bool> filter = null
    ) where T : class
    {
        generator ??= (type, _) => Activator.CreateInstance(type) as T;
        filter ??= _ => true;


        var interfaceType = typeof(T);
        return interfaceType
            .GetAssembly()
            .GetTypes()
            .Where(t => t.GetInterfaces().Contains(interfaceType) && !t.IsInterface)
            .Where(filter)
            .ToDictionary(
                keyGenerator,
                type => new Func<ImageResizeOptions, T>(opts => generator(type, opts)),
                StringComparer.OrdinalIgnoreCase
            );
    }

    private readonly Dictionary<string, Action<Image, Stream, ImageResizeOptions>>
        _reEncoders =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [JpegFormat.Instance.Name] = ReEncodeAsJpeg,
                [PngFormat.Instance.Name] = ReEncodeAsPng,
                [BmpFormat.Instance.Name] = ReEncodeAsJpeg,
                [GifFormat.Instance.Name] = ReEncodeAsGif,
                [BmpFormat.Instance.Name] = ReEncodeAsBmp,
                [WebpFormat.Instance.Name] = ReEncodeAsWebp
            };

    private static void ReEncodeAsWebp(
        Image source,
        Stream target,
        ImageResizeOptions options
    )
    {
        source.SaveAsWebp(
            target,
            new WebpEncoder()
            {
                Method = WebpEncodingMethod.Fastest,
                Quality = options.Quality,
                SkipMetadata = false,
                NearLossless = false
            }
        );
    }

    private static void ReEncodeAsBmp(
        Image source,
        Stream target,
        ImageResizeOptions options
    )
    {
        source.SaveAsBmp(
            target,
            new BmpEncoder()
            {
                BitsPerPixel = DetermineBmpBitDepthFor(options.BitDepth)
            }
        );
    }

    private static void ReEncodeAsGif(
        Image source,
        Stream target,
        ImageResizeOptions options
    )
    {
        source.SaveAsGif(
            target,
            new GifEncoder()
            {
                Quantizer = DetermineQuantizerFor(options),
                ColorTableMode = options.GifColorTableMode
            }
        );
    }

    private static void ReEncodeAsPng(
        Image source,
        Stream target,
        ImageResizeOptions options
    )
    {
        source.SaveAsPng(
            target,
            new PngEncoder()
            {
                Gamma = options.Gamma,
                Quantizer = DetermineQuantizerFor(options),
                Threshold = options.TransparencyThreshold ?? byte.MaxValue,
                BitDepth = DeterminePngBitDepthFor(options.BitDepth),
                ColorType = options.PngColorType,
                CompressionLevel = options.CompressionLevel.AsPngCompressionLevel(),
                FilterMethod = options.PngFilterMethod
            }
        );
    }

    private static void ReEncodeAsJpeg(
        Image source,
        Stream target,
        ImageResizeOptions options
    )
    {
        source.SaveAsJpeg(
            target,
            new JpegEncoder()
            {
                Quality = options.Quality,
                ColorType = options.JpegEncodingColor
            }
        );
    }

    private static BmpBitsPerPixel? DetermineBmpBitDepthFor(int? optionsBitDepth)
    {
        var name = $"Pixel{optionsBitDepth}";
        return TryGetEnumValue<BmpBitsPerPixel>(name);
    }

    private static PngBitDepth? DeterminePngBitDepthFor(int? bitDepth)
    {
        if (bitDepth is null)
        {
            return null;
        }

        var name = $"Bit{bitDepth}";
        return TryGetEnumValue<PngBitDepth>(name);
    }

    private static T? TryGetEnumValue<T>(string value)
        where T : struct
    {
        if (value is null)
        {
            return null;
        }

        return Enum.TryParse<T>(value, out var result)
            ? result
            : null;
    }

    // these are here as failsafes - the image resize
    // parameters should have already set defaults
    // but if the defaults are invalid, we can
    // fall back on valid values rather than
    // simply refusing to do anything
    private static IResampler CreateDefaultResampler()
    {
        return new BicubicResampler();
    }

    private static IQuantizer CreateDefaultQuantizer()
    {
        return new WuQuantizer();
    }
}

public static class ConfigurationExtensions
{
    public static PngCompressionLevel AsPngCompressionLevel(
        this int? compressionLevel
    )
    {
        if (!compressionLevel.HasValue)
        {
            return PngCompressionLevel.DefaultCompression;
        }

        try
        {
            return (PngCompressionLevel) compressionLevel.Value;
        }
        catch
        {
            return PngCompressionLevel.DefaultCompression;
        }
    }

    public static QuantizerOptions AsQuantizerOptions(
        this ImageResizeOptions arg
    )
    {
        var opts = new QuantizerOptions();
        if (arg.MaxColors.HasValue)
        {
            opts.MaxColors = arg.MaxColors.Value;
        }

        if (arg.MaxColors.HasValue)
        {
            opts.MaxColors = arg.MaxColors.Value;
        }

        return opts;
    }
}