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
public class TestConcurrencyMiddleware : TestBase
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class WhenNoConcurrentRequestMatchingQueryString : TestBase
    {
        [Test]
        public async Task ShouldRunNext()
        {
            // Arrange
            var context = new FakeHttpContext();
            var queryString = "?url=http://foo.bar";
            context.Request.QueryString = new QueryString(queryString);
            var invoked = false;
            var next = new Func<HttpContext, Task>(
                ctx =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                }
            );

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
    public class WhenMatchingRequestAlreadyInProgress : TestBase
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
            var next1 = new Func<HttpContext, Task>(
                ctx =>
                {
                    startBarrier.SignalAndWait();
                    Thread.Sleep(1000);
                    invoked++;
                    completionBarrier.SignalAndWait();
                    return Task.CompletedTask;
                }
            );
            var next2 = new Func<HttpContext, Task>(
                ctx =>
                {
                    Thread.Sleep(1000);
                    invoked++;
                    completionBarrier.SignalAndWait();
                    return Task.CompletedTask;
                }
            );
            var appSettings = CreateAppSettings(1, true);

            var sut = Create(appSettings);
            // Act
// #pragma warning disable 4014
            Task.Run(async () => await sut.InvokeAsync(context1, new RequestDelegate(next1)));
            Task.Run(
                async () =>
                {
                    startBarrier.SignalAndWait();
                    await sut.InvokeAsync(context2, new RequestDelegate(next2));
                }
            );
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
        public void ShouldNotEjectAnInProgressCacheableRequestWhenAConcurrentNoStoreRequestCompletes()
        {
            // Arrange
            var context1 = new FakeHttpContext();
            var context2 = new FakeHttpContext();
            var context3 = new FakeHttpContext();
            var queryString = "?url=http://foo.bar";
            context1.Request.QueryString = new QueryString(queryString);
            context2.Request.QueryString = new QueryString(queryString);
            context2.Request.Headers.Append("Cache-Control", "no-store");
            context3.Request.QueryString = new QueryString(queryString);

            var invoked1 = 0;
            var invoked2 = 0;
            var invoked3 = 0;
            var expectedBody = GetRandomBytes(16, 32);

            using var request1Started = new ManualResetEventSlim(false);
            using var releaseRequest1 = new ManualResetEventSlim(false);

            var next1 = new Func<HttpContext, Task>(
                async ctx =>
                {
                    // by the time this runs, request1 has already registered
                    // itself in CurrentRequests
                    request1Started.Set();
                    invoked1++;
                    await ctx.Response.Body.WriteAsync(expectedBody, 0, expectedBody.Length);
                    releaseRequest1.Wait(10000);
                }
            );
            var next2 = new Func<HttpContext, Task>(
                ctx =>
                {
                    invoked2++;
                    return Task.CompletedTask;
                }
            );
            var next3 = new Func<HttpContext, Task>(
                ctx =>
                {
                    invoked3++;
                    return Task.CompletedTask;
                }
            );

            // allow request1 (held open, simulating "still in-flight") and
            // request2 (no-store) to run at the same time
            var appSettings = CreateAppSettings(2, true);
            var sut = Create(appSettings);

            // Act
            var task1 = Task.Run(() => sut.InvokeAsync(context1, new RequestDelegate(next1)));
            Expect(request1Started.Wait(10000))
                .To.Be.True("request1 should have started (and registered itself) by now");

            var task2 = Task.Run(() => sut.InvokeAsync(context2, new RequestDelegate(next2)));
            Expect(task2.Wait(10000))
                .To.Be.True("the no-store request should complete independently of request1");

            // request1 is still in-flight at this point: a 3rd request for the
            // same query should join it rather than triggering a fresh fetch
            var task3 = Task.Run(() => sut.InvokeAsync(context3, new RequestDelegate(next3)));
            // give request3 a chance to (incorrectly) run to completion if
            // request2's no-store handling wrongly evicted request1's entry
            Thread.Sleep(200);
            Expect(invoked3)
                .To.Equal(
                    0,
                    "request3 should be waiting on request1's in-progress result, not performing its own fetch"
                );

            releaseRequest1.Set();
            Expect(task1.Wait(10000))
                .To.Be.True();
            Expect(task3.Wait(10000))
                .To.Be.True();

            // Assert
            Expect(invoked1)
                .To.Equal(1);
            Expect(invoked2)
                .To.Equal(1);
            Expect(invoked3)
                .To.Equal(0);

            var result1 = context1.Response.Body.ReadAllBytes();
            var result3 = context3.Response.Body.ReadAllBytes();
            Expect(result1)
                .To.Equal(expectedBody);
            Expect(result3)
                .To.Equal(expectedBody);
        }

        [TestFixture]
        public class WhenRequestIncludesHeader_CacheControlNoStore
        {
            [Test]
            public void ShouldCallNextForEachRequest()
            {
                // Arrange
                var context1 = new FakeHttpContext();
                var context2 = new FakeHttpContext();
                var queryString = "?url=http://foo.bar";
                context1.Request.QueryString = new QueryString(queryString);
                context1.Request.Headers.Append("Cache-Control", "no-store");
                context2.Request.QueryString = new QueryString(queryString);
                context2.Request.Headers.Append("Cache-Control", "no-store");
                var invoked = 0;
                var startBarrier = new Barrier(3);
                var completionBarrier = new Barrier(3);
                var next1 = new Func<HttpContext, Task>(
                    ctx =>
                    {
                        startBarrier.SignalAndWait();
                        Thread.Sleep(1000);
                        invoked++;
                        return Task.CompletedTask;
                    }
                );
                var next2 = new Func<HttpContext, Task>(
                    ctx =>
                    {
                        Thread.Sleep(1000);
                        invoked++;
                        return Task.CompletedTask;
                    }
                );
                var appSettings = CreateAppSettings(1, true);

                var sut = Create(appSettings);
                // Act
// #pragma warning disable 4014
                Task.Run(async () => { 
                    await sut.InvokeAsync(context1, new RequestDelegate(next1)); 
                    completionBarrier.SignalAndWait();
                });
                Task.Run(
                    async () =>
                    {
                        startBarrier.SignalAndWait();
                        await sut.InvokeAsync(context2, new RequestDelegate(next2));
                        completionBarrier.SignalAndWait();
                    }
                );
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
                    .To.Equal(2);
            }
        }

        [TestFixture]
        public class WhenRequestIncludesHeader_CacheControlWithMultipleDirectivesIncludingNoStore
        {
            [Test]
            public void ShouldTreatTheRequestAsNoStore()
            {
                // Arrange
                var context1 = new FakeHttpContext();
                var context2 = new FakeHttpContext();
                var queryString = "?url=http://foo.bar";
                context1.Request.QueryString = new QueryString(queryString);
                context1.Request.Headers.Append("Cache-Control", "no-cache, no-store");
                context2.Request.QueryString = new QueryString(queryString);
                context2.Request.Headers.Append("Cache-Control", "no-cache, no-store");
                var invoked = 0;
                var startBarrier = new Barrier(3);
                var completionBarrier = new Barrier(3);
                var next1 = new Func<HttpContext, Task>(
                    ctx =>
                    {
                        startBarrier.SignalAndWait();
                        Thread.Sleep(1000);
                        invoked++;
                        return Task.CompletedTask;
                    }
                );
                var next2 = new Func<HttpContext, Task>(
                    ctx =>
                    {
                        Thread.Sleep(1000);
                        invoked++;
                        return Task.CompletedTask;
                    }
                );
                var appSettings = CreateAppSettings(1, true);

                var sut = Create(appSettings);
                // Act
// #pragma warning disable 4014
                Task.Run(async () => {
                    await sut.InvokeAsync(context1, new RequestDelegate(next1));
                    completionBarrier.SignalAndWait();
                });
                Task.Run(
                    async () =>
                    {
                        startBarrier.SignalAndWait();
                        await sut.InvokeAsync(context2, new RequestDelegate(next2));
                        completionBarrier.SignalAndWait();
                    }
                );
// #pragma warning restore 4014

                var timeout = 10000;
                var started = startBarrier.SignalAndWait(timeout);
                var completed = completionBarrier.SignalAndWait(timeout);
                // Assert
                Expect(started)
                    .To.Be.True("Should have started");
                Expect(completed)
                    .To.Be.True("Should have completed");
                // if the compound Cache-Control value wasn't recognised as
                // no-store, request2 would have reused request1's result
                // instead of calling next2 itself
                Expect(invoked)
                    .To.Equal(2);
            }
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
            var next1 = new Func<HttpContext, Task>(
                ctx =>
                {
                    startBarrier.SignalAndWait();
                    Thread.Sleep(1000);
                    invoked++;
                    completionBarrier.SignalAndWait();
                    return Task.CompletedTask;
                }
            );
            var next2 = new Func<HttpContext, Task>(
                ctx =>
                {
                    Thread.Sleep(1000);
                    invoked++;
                    completionBarrier.SignalAndWait();
                    return Task.CompletedTask;
                }
            );

            var sut = Create();
            // Act
            Task.Run(() => sut.InvokeAsync(context1, new RequestDelegate(next1)));
            Task.Run(
                () =>
                {
                    startBarrier.SignalAndWait();
                    sut.InvokeAsync(context2, new RequestDelegate(next2));
                }
            );

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
        var next = new Func<HttpContext, Task>(
            ctx =>
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
            }
        );

        // Act
        var threads = new List<Thread>();
        for (var i = 0; i < requests; i++)
        {
            var t = new Thread(
                () => sut.InvokeAsync(
                    CreateContext(),
                    next.AsRequestDelegate()
                )
            );
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