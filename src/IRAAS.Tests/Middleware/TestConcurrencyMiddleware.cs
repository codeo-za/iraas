using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IRAAS.Middleware;
using IRAAS.Tests.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NSubstitute;
using PeanutButter.Utils;

namespace IRAAS.Tests.Middleware;

[TestFixture]
public class TestConcurrencyMiddleware: TestBase
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WhenNoConcurrentRequestMatchingQueryString: TestBase
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
            Expect(invoked)
                .To.Be.True();
        }
    }

    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WhenMatchingRequestAlreadyInProgress: TestBase
    {
        public static IEnumerable<int> TestRange()
        {
            for (var i = 0; i < 10; i++)
            {
                yield return i;
            }
        }

        [Test]
        public void ShouldOnlyCallNextOnce()
        {
            // Arrange
            var context1 = new FakeHttpContext();
            var context2 = new FakeHttpContext();
            var queryString = "?url=http://foo.bar";
            context1.Request.QueryString = new QueryString(queryString);
            context2.Request.QueryString = new QueryString(queryString);
            var invoked = 0;
            var startBarrier = new Barrier(3);
            var completionBarrier = new Barrier(2);
            var next1 = new Func<HttpContext, Task>(ctx =>
            {
                startBarrier.SignalAndWait();
                Thread.Sleep(1000);
                invoked++;
                completionBarrier.SignalAndWait();
                return Task.CompletedTask;
            });
            var next2 = new Func<HttpContext, Task>(ctx =>
            {
                Thread.Sleep(1000);
                invoked++;
                completionBarrier.SignalAndWait();
                return Task.CompletedTask;
            });
            var appSettings = CreateAppSettings(1, false);

            var sut = Create(appSettings);
            // Act
// #pragma warning disable 4014
            Task.Run(async () => await sut.InvokeAsync(context1, new RequestDelegate(next1)));
            Task.Run(async () =>
            {
                startBarrier.SignalAndWait();
                await sut.InvokeAsync(context2, new RequestDelegate(next2));
            });
// #pragma warning restore 4014

            var timeout = 10000;
            var started = startBarrier.SignalAndWait(timeout);
            var completed = completionBarrier.SignalAndWait(timeout);
            // Assert
            Expect(started)
                .To.Be.True("Should have started");
            Expect(completed)
                .To.Be.True("Should have completed");
            Expect(invoked)
                .To.Equal(1);
        }

        [Test]
        [Parallelizable(ParallelScope.None)]
        public void ShouldHaveTheSameResult()
        {
            // Arrange
            var context1 = new FakeHttpContext();
            var context2 = new FakeHttpContext();
            context1.Response.Body = new MemoryStream();
            context2.Response.Body = new MemoryStream();
            var queryString = "?url=http://foo.bar";
            context1.Request.QueryString = new QueryString(queryString);
            context2.Request.QueryString = new QueryString(queryString);
            var invoked = 0;
            var startBarrier = new Barrier(3);
            var completionBarrier = new Barrier(2);
            var next1 = new Func<HttpContext, Task>(ctx =>
            {
                startBarrier.SignalAndWait();
                Thread.Sleep(1000);
                invoked++;
                completionBarrier.SignalAndWait();
                return Task.CompletedTask;
            });
            var next2 = new Func<HttpContext, Task>(ctx =>
            {
                Thread.Sleep(1000);
                invoked++;
                completionBarrier.SignalAndWait();
                return Task.CompletedTask;
            });

            var sut = Create();
            // Act
            Task.Run(() => sut.InvokeAsync(context1, new RequestDelegate(next1)));
            Task.Run(() =>
            {
                startBarrier.SignalAndWait();
                sut.InvokeAsync(context2, new RequestDelegate(next2));
            });

            var started = startBarrier.SignalAndWait(5000);
            var completed = completionBarrier.SignalAndWait(5000);
            // Assert
            Expect(started)
                .To.Be.True(() => "Did not start all tasks within 5 seconds");
            Expect(completed)
                .To.Be.True(() => "Did not complete all tasks within 5 seconds");
            Expect(invoked)
                .To.Equal(1);

            var result1 = context1.Response.Body.ReadAllBytes();
            var result2 = context2.Response.Body.ReadAllBytes();
            Expect(result1)
                .To.Equal(result2);
        }
    }

    [Test]
    public void ShouldLimitConcurrencyPerConfiguration()
    {
        // Arrange
        var config = CreateAppSettings(1);
        var sut = Create(config);
        var requests = GetRandomInt(10, 20);
        var running = false;
        var failed = false;
        var next = new Func<HttpContext, Task>(ctx =>
        {
            Thread.Sleep(GetRandomInt(0, 50));
            if (running)
            {
                failed = true;
            }
            else
            {
                running = true;
                Thread.Sleep(GetRandomInt(100, 500));
                running = false;
            }

            return Task.CompletedTask;
        });

        // Act
        var threads = new List<Thread>();
        for (var i = 0; i < requests; i++)
        {
            var t = new Thread(() => sut.InvokeAsync(
                CreateContext(),
                next.AsRequestDelegate()
            ));
            threads.Add(t);
        }

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        // Assert
        Expect(failed)
            .To.Be.False();
    }

    private static HttpContext CreateContext()
    {
        return new FakeHttpContext()
        {
            Request =
            {
                QueryString = new QueryString($"?url={GetRandomString(10)}")
            }
        };
    }

    [TestFixture]
    public class LoggingRequests
    {
        // generating the info log isn't free
        // -> disable totally when log level > Information
        // FIXME: tests are a bit anemic
        [Test]
        public async Task ShouldNotLogRequestsWhenNotEnabled()
        {
            // Arrange
            var appSettings = CreateAppSettings(GetRandomInt(1, 20));
            var logger = Substitute.For<ILogger<ConcurrencyMiddleware>>();
            appSettings.IRAASLogLevel.Returns(LogLevel.Warning);
            var sut = Create(appSettings, logger);
            var next = new Func<HttpContext, Task>(ctx => Task.CompletedTask);
            // Act
            await sut.InvokeAsync(
                CreateContext(),
                next.AsRequestDelegate()
            );
            // Assert
            Expect(logger.ReceivedCalls())
                .To.Be.Empty();
        }

        [Test]
        public async Task ShouldLogRequestsWhenEnabled()
        {
            // Arrange
            var appSettings = CreateAppSettings(GetRandomInt(1, 20));
            var logger = Substitute.For<ILogger<ConcurrencyMiddleware>>();
            appSettings.IRAASLogLevel.Returns(LogLevel.Information);
            var sut = Create(appSettings, logger);
            var next = new Func<HttpContext, Task>(ctx => Task.CompletedTask);
            // Act
            await sut.InvokeAsync(
                CreateContext(),
                next.AsRequestDelegate()
            );
            // Assert
            Expect(logger.ReceivedCalls())
                .Not.To.Be.Empty();
        }
    }

    private static ConcurrencyMiddleware Create(
        IAppSettings appSettings = null,
        ILogger<ConcurrencyMiddleware> logger = null)
    {
        return new ConcurrencyMiddleware(
            appSettings ?? CreateAppSettings(1),
            logger ?? Substitute.For<ILogger<ConcurrencyMiddleware>>()
        );
    }

    private static IAppSettings CreateAppSettings(
        int maxConcurrency,
        bool? shareRequests = null
    )
    {
        var result = Substitute.For<IAppSettings>();
        result.MaxConcurrency.Returns(maxConcurrency);
        result.ShareConcurrentRequests.Returns(shareRequests ?? true);
        return result;
    }
}
