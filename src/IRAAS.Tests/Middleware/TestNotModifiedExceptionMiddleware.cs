using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Castle.Core.Logging;
using IRAAS.Exceptions;
using IRAAS.ImageProcessing;
using IRAAS.Logging;
using IRAAS.Middleware;
using IRAAS.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NExpect;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.Utils;
using static NExpect.Expectations;
using static PeanutButter.RandomGenerators.RandomValueGen;
using ILogger = Castle.Core.Logging.ILogger;

namespace IRAAS.Tests.Middleware
{
    [TestFixture]
    public class TestNotModifiedExceptionMiddleware
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
                var logger = new FakeLogger();
                var expectedMessage = GetRandomWords();
                var messageGenerator = Substitute.For<ILogMessageGenerator>();
                messageGenerator.GenerateMessageFor(Arg.Any<NotModifiedException>())
                    .Returns(_ => expectedMessage);
                context.RequestServices = Substitute.For<IServiceProvider>()
                    .With(
                        o => o.GetService<ILogger<NotModifiedException>>()
                            .Returns(_ => (object) logger)
                    )
                    .With(
                        o => o.GetService<ILogMessageGenerator>()
                            .Returns(_ => (object) messageGenerator)
                    );
                var expected = 304;
                Expect(context.Response.StatusCode)
                    .Not.To.Equal(expected);
                // Act
                await sut.InvokeAsync(
                    context,
                    ctx => Task.FromException(new NotModifiedException())
                );
                // Assert
                Expect(context.Response.StatusCode)
                    .To.Equal(expected);
                context.Response.Body.Rewind();
                Expect(
                    Encoding.UTF8.GetString(
                        context.Response.Body.ReadAllBytes()
                    )
                ).To.Be.Empty();

                Expect(logger.History)
                    .To.Contain.None
                    .Matched.By(o => o.LogLevel == LogLevel.Error);
                Expect(logger.History)
                    .To.Contain.Only(1)
                    .Matched.By(
                        o => o.LogLevel == LogLevel.Information &&
                            o.Get<string>("Message") == expectedMessage
                    );
            }

            public class FakeLogger : ILogger<NotModifiedException>
            {
                public LogItem[] History => _history.ToArray();
                private readonly List<LogItem> _history = new();

                public IDisposable BeginScope<TState>(TState state)
                {
                    return new NullDisposable();
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return true;
                }

                public void Log<TState>(
                    LogLevel logLevel,
                    EventId eventId,
                    TState state,
                    Exception exception,
                    Func<TState, Exception, string> formatter
                )
                {
                    _history.Add(
                        new LogItem<TState>(
                            logLevel,
                            eventId,
                            state,
                            exception,
                            formatter
                        )
                    );
                }

                public class LogItem
                {
                    public LogItem(
                        LogLevel logLevel,
                        EventId eventId,
                        Exception exception
                    )
                    {
                        LogLevel = logLevel;
                        EventId = eventId;
                        Exception = exception;
                    }

                    public LogLevel LogLevel { get; }
                    public EventId EventId { get; }
                    public Exception Exception { get; }
                }

                public class LogItem<TState> : LogItem
                {
                    public string Message =>
                        _message ??= Formatter(State, Exception);

                    private string _message;

                    public TState State { get; }
                    public Func<TState, Exception, string> Formatter { get; }

                    public LogItem(
                        LogLevel logLevel,
                        EventId eventId,
                        TState state,
                        Exception exception,
                        Func<TState, Exception, string> formatter
                    ) : base(logLevel, eventId, exception)
                    {
                        State = state;
                        Formatter = formatter;
                    }
                }

                public class NullDisposable : IDisposable
                {
                    public void Dispose()
                    {
                    }
                }
            }
        }

        private static NotModifiedExceptionMiddleware Create()
        {
            return new NotModifiedExceptionMiddleware(Substitute.For<IAppSettings>());
        }
    }
}