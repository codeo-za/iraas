using System;
using System.Text;
using System.Threading.Tasks;
using IRAAS.Middleware;
using IRAAS.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.RandomGenerators;
using PeanutButter.Utils;

namespace IRAAS.Tests.Middleware;

[TestFixture]
public class TestProductionFallbackExceptionMiddleware
{
    [Test]
    public async Task ShouldRunNext()
    {
        // Arrange
        var context = new FakeHttpContext();
        var queryString = "?url=http://foo.bar";
        context.Request.QueryString = new QueryString(queryString);
        var invoked = false;
        var next = new Func<HttpContext, Task>(ctx =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        var sut = Create();
        // Act
        await sut.InvokeAsync(context, new RequestDelegate(next));
        // Assert
        Expectations.Expect(invoked).To.Be.True();
    }

    [Test]
    public async Task ShouldSetShortMessageOnError()
    {
        // Arrange
        var context = new FakeHttpContext();
        var queryString = "?url=http://foo.bar";
        context.Request.QueryString = new QueryString(queryString);
        var next = new Func<HttpContext, Task>(
            ctx => throw new Exception(RandomValueGen.GetRandomString())
        );

        var sut = Create();
        // Act
        await sut.InvokeAsync(context, new RequestDelegate(next));
        // Assert
        Expectations.Expect(context.Response.StatusCode)
            .To.Equal(500);
        var responseBytes = context.Response.Body.ReadAllBytes();
        var responseText = Encoding.UTF8.GetString(responseBytes);
        Expectations.Expect(responseText)
            .To.Be.Empty();
    }

    private ProductionFallbackExceptionHandlerMiddleware Create(
        ILogger<ProductionFallbackExceptionHandlerMiddleware> logger = null)
    {
        return new ProductionFallbackExceptionHandlerMiddleware(
            logger ?? Substitute.For<ILogger<ProductionFallbackExceptionHandlerMiddleware>>()
        );
    }
}