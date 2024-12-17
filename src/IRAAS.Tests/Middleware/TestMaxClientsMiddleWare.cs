using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IRAAS.Middleware;
using IRAAS.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace IRAAS.Tests.Middleware;

[TestFixture]
public class TestMaxClientsMiddleWare
{
    [TestFixture]
    public class WhenOnlyOneClientAllowed
    {
        [Test]
        public async Task ShouldCallNextForOneClient()
        {
            // Arrange
            var nextCalledContext = null as HttpContext;
            var next = new Func<HttpContext, Task>(ctx =>
            {
                nextCalledContext = ctx;
                return Task.CompletedTask;
            });
            var appSettings = CreateAppSettings(1);
            var httpContext = new FakeHttpContext();
            var sut = Create(appSettings);
            // Act
            await sut.InvokeAsync(
                httpContext,
                next.AsRequestDelegate()
            );
            // Assert
            Expect(nextCalledContext)
                .To.Be(httpContext);
        }

        [Test]
        public async Task ShouldAllowTwoSubsequentClients()
        {
            // Arrange
            var captured = new List<HttpContext>();
            var next = new Func<HttpContext, Task>(ctx =>
            {
                captured.Add(ctx);
                return Task.CompletedTask;
            });
            var appSettings = CreateAppSettings(1);
            var httpContext1 = new FakeHttpContext();
            var httpContext2 = new FakeHttpContext();
            var sut = Create(appSettings);

            // Act
            await sut.InvokeAsync(
                httpContext1,
                next.AsRequestDelegate()
            );
            await sut.InvokeAsync(
                httpContext2,
                next.AsRequestDelegate()
            );
            // Assert
            Expect(captured)
                .To.Equal(new[] { httpContext1, httpContext2 });
        }

        [Test]
        public async Task ShouldBatSecondConcurrentClient()
        {
            // Arrange
            var captured = new ConcurrentBag<HttpContext>();
            var barrier1 = new Barrier(2);
            var barrier2 = new Barrier(2);
            var next1 = new Func<HttpContext, Task>(ctx =>
            {
                barrier1.SignalAndWait();
                barrier2.SignalAndWait();
                captured.Add(ctx);
                return Task.CompletedTask;
            });
            var next2 = new Func<HttpContext, Task>(ctx =>
            {
                captured.Add(ctx);
                return Task.CompletedTask;
            });
            var appSettings = CreateAppSettings(1);
            var httpContext1 = new FakeHttpContext();
            var httpContext2 = new FakeHttpContext();
            var sut = Create(appSettings);

            // Act
            var task1 = Task.Run(async () =>
            {
                await sut.InvokeAsync(
                    httpContext1,
                    next1.AsRequestDelegate()
                );
            });
            barrier1.SignalAndWait();

            var task2 = sut.InvokeAsync(
                httpContext2,
                next2.AsRequestDelegate()
            );

            barrier2.SignalAndWait();
            await Task.WhenAll(task1, task2);
            // Assert

            Expect(captured.ToArray())
                .To.Equal(new[] { httpContext1 });
            Expect(httpContext2.Response.StatusCode)
                .To.Equal((int) HttpStatusCode.ServiceUnavailable);
        }

        [Test]
        public async Task ZeroMaxClientsShouldNotThrottleClients()
        {
            // Arrange
            var captured = new ConcurrentBag<HttpContext>();
            var barrier1 = new Barrier(2);
            var barrier2 = new Barrier(2);
            var next1 = new Func<HttpContext, Task>(ctx =>
            {
                barrier1.SignalAndWait();
                barrier2.SignalAndWait();
                captured.Add(ctx);
                return Task.CompletedTask;
            });
            var next2 = new Func<HttpContext, Task>(ctx =>
            {
                captured.Add(ctx);
                return Task.CompletedTask;
            });
            var appSettings = CreateAppSettings(0);
            var httpContext1 = new FakeHttpContext();
            var httpContext2 = new FakeHttpContext();
            var sut = Create(appSettings);

            // Act
            var task1 = Task.Run(async () =>
            {
                await sut.InvokeAsync(
                    httpContext1,
                    next1.AsRequestDelegate()
                );
            });
            barrier1.SignalAndWait();

            var task2 = sut.InvokeAsync(
                httpContext2,
                next2.AsRequestDelegate()
            );

            barrier2.SignalAndWait();
            await Task.WhenAll(task1, task2);
            // Assert

            Expect(captured.ToArray())
                .To.Be.Equivalent.To(new[] { httpContext1, httpContext2 });
            Expect(httpContext2.Response.StatusCode)
                .Not.To.Equal((int) HttpStatusCode.ServiceUnavailable);
        }
    }

    private static IAppSettings CreateAppSettings(
        int maxClients
    )
    {
        var result = Substitute.For<IAppSettings>();
        result.MaxClients.Returns(maxClients);
        return result;
    }

    private static MaxClientsMiddleware Create(
        IAppSettings appSettings,
        ILogger<ConcurrencyMiddleware> logger = null
    )
    {
        return new MaxClientsMiddleware(
            appSettings,
            logger
        );
    }
}