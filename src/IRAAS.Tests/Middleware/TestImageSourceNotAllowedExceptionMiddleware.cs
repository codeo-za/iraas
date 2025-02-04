using System;
using System.Text;
using System.Threading.Tasks;
using IRAAS.Exceptions;
using IRAAS.Middleware;
using IRAAS.Tests.Fakes;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.Utils;

namespace IRAAS.Tests.Middleware;

[TestFixture]
public class TestImageSourceNotAllowedExceptionMiddleware: TestBase
{
    [TestFixture]
    public class WhenNoExceptionThrown: TestBase
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
    public class WhenAnotherExceptionIsThrown: TestBase
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
    public class WhenImageSourceNotAllowedExceptionThrown: TestBase
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