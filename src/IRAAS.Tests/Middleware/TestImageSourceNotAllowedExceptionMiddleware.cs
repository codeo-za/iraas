using System;
using System.Text;
using System.Threading.Tasks;
using IRAAS.Exceptions;
using IRAAS.Middleware;
using IRAAS.Tests.Fakes;
using NExpect;
using NExpect.Interfaces;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.Utils;
using static NExpect.Expectations;
using static PeanutButter.RandomGenerators.RandomValueGen;

namespace IRAAS.Tests.Middleware
{
    [TestFixture]
    public class TestImageSourceNotAllowedExceptionMiddleware
    {
        [TestFixture]
        public class WhenNoExceptionThrown
        {
            [Test]
            public async Task ShouldNotInterfereWithTheResponse()
            {
                // Arrange
                var sut = Create();
                var expected = GetRandomInt(200, 299);
                var context = new FakeHttpContext();
                // Act
                await sut.InvokeAsync(
                    context,
                    ctx => Task.Run(
                        () =>
                        {
                            ctx.Response.StatusCode = expected;
                        }
                    )
                );
                // Assert
                Expect(context.Response.StatusCode)
                    .To.Equal(expected);
            }
        }

        [TestFixture]
        public class WhenAnotherExceptionIsThrown
        {
            [Test]
            public void ShouldNotInterfere()
            {
                // Arrange
                var sut = Create();
                var expected = GetRandomInt(200, 299);
                var context = new FakeHttpContext();
                var ex = GetRandomFrom(
                    new Exception[]
                    {
                        new ArgumentException(GetRandomString()),
                        new InvalidOperationException(GetRandomString()),
                        new ApplicationException(GetRandomString())
                    }
                );
                // Act
                Expect(
                    () =>
                        sut.InvokeAsync(
                            context,
                            ctx => Task.FromException(ex)
                        )
                ).To.Throw().With.Type(ex.GetType());
                // Assert
            }
        }

        [TestFixture]
        public class WhenImageSourceNotAllowedExceptionThrown
        {
            [Test]
            public async Task ShouldSetResultStatusCodeTo_403()
            {
                // Arrange
                var sut = Create();
                var context = new FakeHttpContext();
                var expected = 403;
                var url = GetRandomHttpUrl();
                Expect(context.Response.StatusCode)
                    .Not.To.Equal(expected);
                // Act
                await sut.InvokeAsync(
                    context,
                    ctx => Task.FromException(new ImageSourceNotAllowedException(url))
                );
                // Assert
                Expect(context.Response.StatusCode)
                    .To.Equal(expected);
                context.Response.Body.Rewind();
                Expect(
                    Encoding.UTF8.GetString(
                        context.Response.Body.ReadAllBytes()
                    )
                ).To.Contain(url);
            }
        }

        private static ImageSourceNotAllowedExceptionMiddleware Create()
        {
            return new ImageSourceNotAllowedExceptionMiddleware(Substitute.For<IAppSettings>());
        }
    }

    [TestFixture]
    public class TestInvalidProcessingOptionsExceptionMiddleware
    {
        [TestFixture]
        public class WhenNoExceptionThrown
        {
            [Test]
            public async Task ShouldNotInterfereWithTheResponse()
            {
                // Arrange
                var sut = Create();
                var expected = GetRandomInt(200, 299);
                var context = new FakeHttpContext();
                // Act
                await sut.InvokeAsync(
                    context,
                    ctx => Task.Run(
                        () =>
                        {
                            ctx.Response.StatusCode = expected;
                        }
                    )
                );
                // Assert
                Expect(context.Response.StatusCode)
                    .To.Equal(expected);
            }
        }

        [TestFixture]
        public class WhenAnotherExceptionIsThrown
        {
            [Test]
            public void ShouldNotInterfere()
            {
                // Arrange
                var sut = Create();
                var expected = GetRandomInt(200, 299);
                var context = new FakeHttpContext();
                var ex = GetRandomFrom(
                    new Exception[]
                    {
                        new ArgumentException(GetRandomString()),
                        new InvalidOperationException(GetRandomString()),
                        new ApplicationException(GetRandomString())
                    }
                );
                // Act
                Expect(
                    () =>
                        sut.InvokeAsync(
                            context,
                            ctx => Task.FromException(ex)
                        )
                ).To.Throw().With.Type(ex.GetType());
                // Assert
            }
        }

        [TestFixture]
        public class WhenImageSourceNotAllowedExceptionThrown
        {
            [Test]
            public async Task ShouldSetResultStatusCodeTo_403()
            {
                // Arrange
                var sut = Create();
                var context = new FakeHttpContext();
                var expectedCode = 400;
                var expectedMessage = GetRandomString(32);
                Expect(context.Response.StatusCode)
                    .Not.To.Equal(expectedCode);
                // Act
                await sut.InvokeAsync(
                    context,
                    ctx => Task.FromException(new InvalidProcessingOptionsException(expectedMessage))
                );
                // Assert
                Expect(context.Response.StatusCode)
                    .To.Equal(expectedCode);
                context.Response.Body.Rewind();
                var body = Encoding.UTF8.GetString(
                    context.Response.Body.ReadAllBytes()
                );
                Expect(body).To.Contain(expectedMessage);
                Expect(body).To.Contain("Query parameters");
            }
        }

        private static InvalidProcessingOptionsExceptionMiddleware Create()
        {
            return new InvalidProcessingOptionsExceptionMiddleware(Substitute.For<IAppSettings>());
        }
    }
}