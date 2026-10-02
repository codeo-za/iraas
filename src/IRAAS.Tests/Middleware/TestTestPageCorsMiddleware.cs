using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IRAAS.Middleware;
using IRAAS.Tests.ImageProcessing;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.TestUtils.AspNetCore.Builders;
using PeanutButter.TestUtils.AspNetCore.Utils;

namespace IRAAS.Tests.Middleware;

[TestFixture]
public class TestTestPageCorsMiddleware
{
    [Test]
    public void ShouldImplementIMiddleware()
    {
        // Arrange
        // Act
        Expect(typeof(TestPageCorsMiddleware))
            .To.Implement<IMiddleware>();
        // Assert
    }

    [TestFixture]
    public class WhenTestPageEnabled
    {
        [TestCase("/")]
        [TestCase("/size")]
        [TestCase("/size/")]
        public async Task ShouldAllowAnyOriginToReadGetResponsesFrom_(
            string path
        )
        {
            // Arrange
            HttpContext captured = null;
            var (ctx, next) = Arena(HttpMethods.Get, path, c => captured = c, out var startResponse);
            var sut = Create(TestPageEnabled());

            // Act
            await sut.InvokeAsync(ctx, next);
            await startResponse();

            // Assert
            Expect(captured)
                .To.Be(ctx);
            var headers = ctx.Response.Headers;
            Expect(headers["Access-Control-Allow-Origin"].ToString())
                .To.Equal("*");
            Expect(headers["Access-Control-Expose-Headers"].ToString())
                .To.Equal("*");
            Expect(headers["Timing-Allow-Origin"].ToString())
                .To.Equal("*");
        }

        [Test]
        public async Task ShouldOverrideCorsHeadersEchoedFromTheImageSource()
        {
            // Arrange
            var (ctx, next) = Arena(
                HttpMethods.Get,
                "/",
                c => c.Response.Headers["Access-Control-Allow-Origin"] = "https://elsewhere.com",
                out var startResponse
            );
            var sut = Create(TestPageEnabled());

            // Act
            await sut.InvokeAsync(ctx, next);
            await startResponse();

            // Assert
            Expect(ctx.Response.Headers["Access-Control-Allow-Origin"].ToString())
                .To.Equal("*");
        }

        [TestCase("POST", "/")]
        [TestCase("GET", "/test")]
        [TestCase("GET", "/health")]
        [TestCase("GET", "/config")]
        public async Task ShouldNotAddCorsHeadersFor_(
            string method,
            string path
        )
        {
            // Arrange
            HttpContext captured = null;
            var (ctx, next) = Arena(method, path, c => captured = c, out var startResponse);
            var sut = Create(TestPageEnabled());

            // Act
            await sut.InvokeAsync(ctx, next);
            await startResponse();

            // Assert
            Expect(captured)
                .To.Be(ctx);
            Expect(ctx.Response.Headers.Keys)
                .Not.To.Contain("Access-Control-Allow-Origin");
        }
    }

    [TestFixture]
    public class WhenTestPageDisabled
    {
        [TestCase("/")]
        [TestCase("/size")]
        public async Task ShouldNotAddCorsHeadersFor_(
            string path
        )
        {
            // Arrange
            HttpContext captured = null;
            var (ctx, next) = Arena(HttpMethods.Get, path, c => captured = c, out var startResponse);
            var appSettings = Substitute.For<IAppSettings>()
                .WithDefaultSettings()
                .WithTestPageDisabled();
            var sut = Create(appSettings);

            // Act
            await sut.InvokeAsync(ctx, next);
            await startResponse();

            // Assert
            Expect(captured)
                .To.Be(ctx);
            var headers = ctx.Response.Headers;
            Expect(headers.Keys)
                .Not.To.Contain("Access-Control-Allow-Origin");
            Expect(headers.Keys)
                .Not.To.Contain("Access-Control-Expose-Headers");
            Expect(headers.Keys)
                .Not.To.Contain("Timing-Allow-Origin");
        }
    }

    private static RequestDelegateTestArena Arena(
        string method,
        string path,
        Action<HttpContext> logic,
        out Func<Task> startResponse
    )
    {
        // the fake response doesn't run OnStarting handlers itself, so
        // capture them and let the test say when the response starts
        var onStarting = new List<(Func<object, Task> callback, object state)>();
        startResponse = async () =>
        {
            foreach (var (callback, state) in onStarting)
            {
                await callback(state);
            }
        };
        return RequestDelegateTestArenaBuilder.Create()
            .WithContext(
                HttpContextBuilder.Create()
                    .WithRequestMethod(method)
                    .WithRequestPath(path)
                    .WithRequestQueryParameter("url", GetRandomHttpsUrlWithPath())
                    .WithResponse(
                        HttpResponseBuilder.Create()
                            .WithOnStartingHandler((callback, state) => onStarting.Add((callback, state)))
                            .Build()
                    )
                    .Build()
            ).WithDelegateLogic(logic)
            .Build();
    }

    private static IAppSettings TestPageEnabled()
    {
        return Substitute.For<IAppSettings>()
            .WithDefaultSettings()
            .WithTestPageEnabled();
    }

    private static TestPageCorsMiddleware Create(
        IAppSettings appSettings
    )
    {
        return new TestPageCorsMiddleware(appSettings);
    }
}
