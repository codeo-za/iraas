using System;
using NExpect.Implementations;
using NExpect.Interfaces;
using NExpect.MatcherLogic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static NExpect.Implementations.MessageHelpers;

namespace IRAAS.Tests
{
    public static class ImageMatchers
    {
        public static IMore<Image> Width(
            this IHave<Image> have,
            int expected)
        {
            return have.Width(expected, null);
        }

        public static IMore<Image> Width(
            this IHave<Image> have,
            int expected,
            Func<string> customMessageGenerator)
        {
            have.AddMatcher(actual =>
            {
                var passed = actual.Width == expected;
                return new MatcherResult(
                    passed,
                    FinalMessageFor(
                        () => $"Expected {passed.AsNot()}to find width {expected} but got {actual.Width}",
                        customMessageGenerator
                    ));
            });
            return have.More();
        }

        public static IMore<Image> Height(
            this IHave<Image> have,
            int expected)
        {
            return have.Height(expected, null);
        }

        public static IMore<Image> Height(
            this IHave<Image> have,
            int expected,
            Func<string> customMessageGenerator)
        {
            have.AddMatcher(actual =>
            {
                var passed = actual.Height == expected;
                return new MatcherResult(
                    passed,
                    FinalMessageFor(
                        () => $"Expected {passed.AsNot()}to find height {expected} but got {actual.Height}",
                        customMessageGenerator
                    ));
            });
            return have.More();
        }
    }
}