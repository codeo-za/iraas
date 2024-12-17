using System;
using System.IO;
using System.Linq;
using IRAAS.Security;
using NUnit.Framework;
using PeanutButter.Utils;

namespace IRAAS.Tests.Security;

[TestFixture]
public class TestLimitedMemoryStream
{
    [Test]
    public void ShouldBeAbleToRead()
    {
        // Arrange
        var sut = Create();
        // Act
        var result = sut.CanRead;
        // Assert
        Expect(result)
            .To.Be.True();
    }

    [Test]
    public void ShouldBeAbleToSeek()
    {
        // Arrange
        var sut = Create();
        // Act
        var result = sut.CanSeek;
        // Assert
        Expect(result)
            .To.Be.True();
    }

    [Test]
    public void ShouldBeAbleToWrite()
    {
        // Arrange
        var sut = Create();
        // Act
        var result = sut.CanWrite;
        // Assert
        Expect(result)
            .To.Be.True();
    }

    [TestFixture]
    public class WhenWithinLimit
    {
        [Test]
        public void ShouldBeAbleToWrite()
        {
            // Arrange
            var data = GetRandomBytes(1024);
            var sut = Create(data.Length + 1);
            // Act
            sut.Write(data, 0, data.Length);
            // Assert
            var result = sut.ToArray();
            Expect(result)
                .To.Equal(data);
        }

        [Test]
        public void ShouldBeAbleToRead()
        {
            // Arrange
            var data = GetRandomBytes(1024);
            var result = new byte[data.Length];
            var sut = Create(data.Length + 1);
            sut.Write(data, 0, data.Length);
            // Act
            sut.Rewind();
            sut.Read(result, 0, data.Length);
            // Assert
            Expect(result)
                .To.Equal(data);
        }

        [Test]
        public void ShouldBeAbleToFlush()
        {
            // Arrange
            var data = GetRandomBytes(1024);
            var sut = Create(data.Length + 1);
            // Act
            sut.Write(data, 0, data.Length);
            // Assert
            Expect(() => sut.Flush())
                .Not.To.Throw();
        }

        [Test]
        public void ShouldBeAbleToSeek()
        {
            // Arrange
            var data = GetRandomBytes(1024);
            var toSkip = GetRandomInt(100, 200);
            var expected = data.Skip(toSkip).ToArray();
            var result = new byte[data.Length - toSkip];
            var sut = Create(data.Length + 1);
            sut.Write(data, 0, data.Length);
            // Act
            sut.Seek(toSkip, SeekOrigin.Begin);
            sut.Read(result, 0, result.Length);
            // Assert
            Expect(result)
                .To.Equal(expected);
        }
    }

    [TestFixture]
    public class WhenExceedingLimit
    {
        [Test]
        public void WriteFromStartShouldThrow()
        {
            // Arrange
            var data = GetRandomBytes(1024);
            var sut = Create(data.Length - 1);
            // Act
            Expect(() => sut.Write(data, 0, data.Length))
                .To.Throw<NotSupportedException>();
            // Assert
        }

        [Test]
        public void WriteFromOffsetShouldThrow()
        {
            // Arrange
            var data1 = GetRandomBytes(512);
            var data2 = GetRandomBytes(512);
            var sut = Create(data1.Length + GetRandomInt(100, 200));
            // Act
            sut.Write(data1, 0, data1.Length);
            Expect(() => sut.Write(data2, 0, data2.Length - GetRandomInt(50, 100)))
                .To.Throw<NotSupportedException>();
            // Assert
        }

        [Test]
        public void SeekFromOriginPastLimit_ShouldThrow()
        {
            // Arrange
            var max = 1024;
            var data = GetRandomBytes(512, max);
            var sut = Create(max);
            // Act
            sut.Write(data, 0, data.Length);
            Expect(() => sut.Seek(1025, SeekOrigin.Begin))
                .To.Throw<NotSupportedException>();
            // Assert
        }

        [Test]
        public void SeekFromCurrentPastLimit_ShouldThrow()
        {
            // Arrange
            var data = GetRandomBytes(512);
            var sut = Create(data.Length);
            // Act
            sut.Write(data, 0, data.Length);
            sut.Seek(100, SeekOrigin.Begin);
            Expect(() => sut.Seek(data.Length - 100 + 1, SeekOrigin.Current))
                .To.Throw<NotSupportedException>();
            // Assert
        }
    }

    private static LimitedMemoryStream Create(
        long? maxSize = null)
    {
        return new LimitedMemoryStream(
            maxSize ?? long.MaxValue);
    }
}
