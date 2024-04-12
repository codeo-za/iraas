using System;
using System.Threading.Tasks;
using IRAAS.Middleware;
using IRAAS.Tests.Fakes;
using NExpect;
using NSubstitute;
using NUnit.Framework;
using static NExpect.Expectations;
using static PeanutButter.RandomGenerators.RandomValueGen;

namespace IRAAS.Tests.Middleware
{
    [TestFixture]
    public class TestNotImplementedExceptionMiddleware
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
                    ctx => Task.Run(() => ctx.Response.StatusCode = expected));
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
                var ex = GetRandomFrom(new Exception[]
                {
                    new ArgumentException(GetRandomString()),
                    new InvalidOperationException(GetRandomString()),
                    new ApplicationException(GetRandomString())
                });
                // Act
                Expect(() =>
                    sut.InvokeAsync(
                        context,
                        ctx => Task.FromException(ex)
                    )
                ).To.Throw().With.Type(ex.GetType());
                // Assert
            }
        }

        [TestFixture]
        public class WhenNotImplementedExceptionThrown
        {
            [Test]
            public async Task ShouldSetResultStatusCodeTo_404()
            {
                // Arrange
                var sut = Create();
                var context = new FakeHttpContext();
                var expected = 404;
                Expect(context.Response.StatusCode)
                    .Not.To.Equal(expected);
                // Act
                await sut.InvokeAsync(
                    context,
                    ctx => Task.FromException(new NotImplementedException())
                );
                // Assert
                Expect(context.Response.StatusCode)
                    .To.Equal(expected);
            }
        }

        private static NotImplementedExceptionMiddleware Create()
        {
            return new NotImplementedExceptionMiddleware(Substitute.For<IAppSettings>());
        }
    }
}