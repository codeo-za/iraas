using System;
using System.IO;
using IRAAS.ImageProcessing;
using NUnit.Framework;
using static PeanutButter.RandomGenerators.RandomValueGen;
using NExpect;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using static NExpect.Expectations;

namespace IRAAS.Tests.ImageProcessing
{
    [TestFixture]
    public class TestImageMimeTypeProvider
    {
        [Test]
        public void ShouldImplement_IImageMimeTypeProvider()
        {
            // Arrange
            // Act
            Expect(typeof(ImageMimeTypeProvider))
                .To.Implement<IImageMimeTypeProvider>();
            // Assert
        }

        [TestFixture]
        [Parallelizable(ParallelScope.None)]
        public class DetermineMimeTypeFor
        {
            [Test]
            public void GivenNullStream_ShouldThrow()
            {
                // Arrange
                var sut = Create();
                // Act
                Expect(() => sut.DetermineMimeTypeFor(null))
                    .To.Throw<ArgumentNullException>();
                // Assert
            }

            [TestCase("image/bmp")]
            public void GivenBitmapStream_ShouldReturn_(string expected)
            {
                // Arrange
                var stream = Resources.Streams.FluffyCatBmp;
                var sut = Create();
                // Act
                var result = sut.DetermineMimeTypeFor(stream);
                // Assert
                Expect(result).To.Equal(expected);
            }
            
            [TestCase("image/png")]
            public void GivenPngStream_ShouldReturn_(string expected)
            {
                // Arrange
                var sut = Create();
                // Act
                var result = sut.DetermineMimeTypeFor(Resources.Streams.FluffyCatPng);
                // Assert
                Expect(result).To.Equal(expected);
            }
            
            [TestCase("image/jpeg")]
            public void GivenJpegStream_ShouldReturn_(string expected)
            {
                // Arrange
                var sut = Create();
                // Act
                var result = sut.DetermineMimeTypeFor(Resources.Streams.FluffyCatJpeg);
                // Assert
                Expect(result).To.Equal(expected);
            }
            
            [TestCase("image/gif")]
            public void GivenGifStream_ShouldReturn_(string expected)
            {
                // Arrange
                var sut = Create();
                // Act
                var result = sut.DetermineMimeTypeFor(Resources.Streams.FluffyCatGif);
                // Assert
                Expect(result).To.Equal(expected);
            }

            [Test]
            public void GivenEmptyStream_ShouldThrow()
            {
                // Arrange
                var sut = Create();
                var stream = new MemoryStream(new byte[0]);
                // Act
                Expect(() => sut.DetermineMimeTypeFor(stream))
                    .To.Throw<NotSupportedException>();
                // Assert
            }

            [Test]
            public void GivenRubbishStream_ShouldThrow()
            {
                // Arrange
                var sut = Create();
                var stream = new MemoryStream(GetRandomBytes(1024, 2048));
                // Act
                Expect(() => sut.DetermineMimeTypeFor(stream))
                    .To.Throw<NotSupportedException>();
                // Assert
            }
        }

        private static IImageMimeTypeProvider Create()
        {
            return new ImageMimeTypeProvider();
        }
    }
}