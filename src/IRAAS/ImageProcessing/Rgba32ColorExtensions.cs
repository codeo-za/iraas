using System;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace IRAAS.ImageProcessing
{
    public static class Rgba32ColorExtensions
    {
        private static readonly Rgba32 White = Rgba32.ParseHex("#FFFFFF");
        private static readonly Rgba32? NoResult = null;

        public static Rgba32 AsRgba32(this string color)
        {
            return Strategies.Aggregate(
                null as Rgba32?,
                (acc, cur) => acc ?? cur(color)
            ) ?? White;
        }

        private static readonly Func<string, Rgba32?>[] Strategies =
        {
            TryParseHex,
            TryParseColor
        };

        private static Rgba32? TryParseHex(string input)
        {
            return Rgba32.TryParseHex(input, out var result)
                ? result
                : NoResult;
        }

        private static Rgba32? TryParseColor(string input)
        {
            return Color.TryParse(input, out var color)
                ? TryParseHex(color.ToHex())
                : NoResult;
        }
    }
}