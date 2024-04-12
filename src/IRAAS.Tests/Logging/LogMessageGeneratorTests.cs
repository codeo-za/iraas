using System;
using IRAAS.Exceptions;
using IRAAS.Logging;
using NUnit.Framework;
using NExpect;
using static NExpect.Expectations;
using static PeanutButter.RandomGenerators.RandomValueGen;

namespace IRAAS.Tests.Logging
{
    [TestFixture]
    public class LogMessageGeneratorTests
    {
        [Test]
        public void ShouldNotBreakOnNull()
        {
            // Arrange
            var sut = Create();
            // Act
            var result = sut.GenerateMessageFor(null);
            // Assert
            Expect(result)
                .To.Equal("(null)");
        }

        [Test]
        public void ShouldIncludeExceptionMessage()
        {
            // Arrange
            var sut = Create();
            var expected = GetRandomWords();
            var ex = new Exception(expected);
            // Act
            var result = sut.GenerateMessageFor(ex);
            // Assert
            Expect(result)
                .To.Start.With(ex.Message);
        }

        [Test]
        public void ShouldNotIncludeExceptionType()
        {
            // log is already made with exception type
            // -> repeating that doesn't help at all
            // Arrange
            var url = GetRandomHttpsUrl();
            var ex = new ImageSourceNotAllowedException(url);
            var sut = Create();
            // Act
            var result = sut.GenerateMessageFor(ex);
            // Assert
            Expect(result)
                .To.Contain($@"""Type"":""{nameof(ImageSourceNotAllowedException)}""");
        }

        [Test]
        public void ShouldIncludeExceptionStacktrace()
        {
            // Arrange
            var url = GetRandomHttpsUrl();
            Exception ex;
            try
            {
                throw new ImageSourceNotAllowedException(url);
            }
            catch (Exception e)
            {
                ex = e;
            }

            var sut = Create();
            // Act
            var result = sut.GenerateMessageFor(ex);
            // Assert
            Expect(result)
                .To.Contain(" exception::{")
                .Then(
                    $@"""StackTrace"":""{ex.StackTrace.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\\", "\\\\")}""");
        }

        [Test]
        public void ShouldLogStackTraceLast()
        {
            // Arrange
            var url = GetRandomHttpsUrl();
            Exception ex;
            try
            {
                throw new ImageSourceNotAllowedException(url);
            }
            catch (Exception e)
            {
                ex = e;
            }

            var sut = Create();
            // Act
            var result = sut.GenerateMessageFor(ex);
            // Assert
            Expect(result)
                .To.Contain(" exception::{")
                .Then(url)
                .Then(
                    $@"""StackTrace"":""{ex.StackTrace.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\\", "\\\\")}""");
        }

        [Test]
        public void ShouldIncludeAnyPropertiesNotFoundOnBaseException()
        {
            // Arrange
            var url = GetRandomHttpsUrl();
            var prop1 = GetRandomString(10);
            var prop2 = GetRandomInt(400, 500);
            var prop3 = GetRandomBoolean();
            var ex = new EnhancedImageSourceNotAllowedException(
                url, prop1, prop2, prop3
            );
            var sut = Create();
            // Act
            var result = sut.GenerateMessageFor(ex);
            // Assert
            Expect(result)
                .To.Contain(" exception::{")
                .Then($@"""Url"":""{url}""");
            Expect(result)
                .To.Contain(" exception::{")
                .Then($@"""Prop1"":""{prop1}""");
            Expect(result)
                .To.Contain(" exception::{")
                .Then($@"""Prop2"":{prop2}");
            Expect(result)
                .To.Contain(" exception::{")
                .Then($@"""Prop3"":{prop3.ToString().ToLower()}");
        }

        public class EnhancedImageSourceNotAllowedException : ImageSourceNotAllowedException
        {
            public string Prop1 { get; }
            public int Prop2 { get; }
            public bool Prop3 { get; }

            public EnhancedImageSourceNotAllowedException(
                string url,
                string prop1,
                int prop2,
                bool prop3
            ) : base(url)
            {
                Prop1 = prop1;
                Prop2 = prop2;
                Prop3 = prop3;
            }
        }

        private static ILogMessageGenerator Create()
        {
            return new LogMessageGenerator();
        }
    }
}