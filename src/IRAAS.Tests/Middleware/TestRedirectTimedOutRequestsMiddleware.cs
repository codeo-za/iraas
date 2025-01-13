using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using IRAAS.ImageProcessing;
using IRAAS.Middleware;
using IRAAS.Tests.Fakes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.Utils;

namespace IRAAS.Tests.Middleware;

[TestFixture]
public class TestRedirectTimedOutRequestsMiddleware: TestBase
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
    public class WhenRequestTimedOutExceptionThrown: TestBase
    {
        [Test]
        public async Task ShouldSetResultStatusCodeTo_301()
        {
            // Arrange
            var logger = Substitute.For<ILogger<RedirectTimedOutRequestsMiddleware>>();
            var sut = Create(logger);
            var context = new FakeHttpContext();
            var expected = 301;
            var url = GetRandomHttpUrl();
            Expect(context.Response.StatusCode)
                .Not.To.Equal(expected);
            // Act
            await sut.InvokeAsync(
                context,
                ctx => Task.FromException(
                    new RequestTimedOutException(
                        url,
                        new Dictionary<string, string>()
                    )
                )
            );
            // Assert
            Expect(context.Response.StatusCode)
                .To.Equal(expected);
            context.Response.Body.Rewind();
            var body = Encoding.UTF8.GetString(
                context.Response.Body.ReadAllBytes()
            );
            Expect(body)
                .To.Contain("Moved");
            Expect(context.Response.Headers.ToDictionary())
                .To.Contain.Key("Location")
                .With.Value(url);
        }
    }

    private static RedirectTimedOutRequestsMiddleware Create(
        ILogger<RedirectTimedOutRequestsMiddleware> logger = null
    )
    {
        return new RedirectTimedOutRequestsMiddleware(
            logger ?? Substitute.For<ILogger<RedirectTimedOutRequestsMiddleware>>(),
            Substitute.For<IAppSettings>()
        );
    }
}