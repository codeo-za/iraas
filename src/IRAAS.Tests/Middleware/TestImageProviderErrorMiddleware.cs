using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using IRAAS.ImageProcessing;
using IRAAS.Middleware;
using IRAAS.Tests.Fakes;
using NUnit.Framework;
using static PeanutButter.RandomGenerators.RandomValueGen;
using NExpect;
using NSubstitute;
using PeanutButter.Utils;
using static NExpect.Expectations;

namespace IRAAS.Tests.Middleware
{
    [TestFixture]
    public class TestImageProviderErrorMiddleware
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
        public class WhenImageProviderErrorExceptionThrown
        {
            [Test]
            public async Task ShouldSetResultStatusCodeToUpstreamCodeWhenAvailable()
            {
                // Arrange
                var sut = Create();
                var context = new FakeHttpContext();
                var url = GetRandomHttpUrl();
                var expectedResponseHeader = GetRandomString(1);
                var expectedResponseHeaderValue = GetRandomString(1);
                var statusCode = GetRandom<HttpStatusCode>();
                var headers = new WebHeaderCollection()
                {
                    { expectedResponseHeader, expectedResponseHeaderValue}
                };
                var expected = (int)statusCode;
                Expect(context.Response.StatusCode)
                    .Not.To.Equal(expected);
#pragma warning disable SYSLIB0014
                var request = WebRequest.Create(url) as HttpWebRequest;
#pragma warning restore SYSLIB0014
                var expectedRequestHeader = GetRandomString(1);
                var expectedRequestHeaderValue = GetRandomString(1);
                request.Headers[expectedRequestHeader] = expectedRequestHeaderValue;

                var response = Substitute.For<HttpWebResponse>();
                response.StatusCode.Returns(statusCode);
                response.Headers.Returns(headers);

                // Act
                await sut.InvokeAsync(
                    context,
                    ctx => Task.FromException(
                        new ImageProviderErrorException(
                            response.StatusCode,
                            request.RequestUri.ToString(),
                            request.Headers.ToDictionary(),
                            response.Headers.ToDictionary()
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
                    .To.Contain("Unable to retrieve image")
                    .And.To.Contain(url)
                    .And.To.Contain("request headers:")
                    .Then($"{expectedRequestHeader}: {expectedRequestHeaderValue}")
                    .Then($"response status: {(int)statusCode}")
                    .Then("response headers:")
                    .Then($"{expectedResponseHeader}: {expectedResponseHeaderValue}");
            }
        }


        private static ImageProviderErrorMiddleware Create()
        {
            return new ImageProviderErrorMiddleware(Substitute.For<IAppSettings>());
        }
    }
}