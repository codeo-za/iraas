using System;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace IRAAS.Tests.Middleware
{
    public class ConsoleLogger<T> : ILogger<T>
    {
        public LogLevel Level { get; }

        public ConsoleLogger(): this(LogLevel.Debug)
        {
        }

        public ConsoleLogger(LogLevel level)
        {
            Level = level;
        }

        public void Log<TState>(
            LogLevel logLevel, 
            EventId eventId, 
            TState state, 
            Exception exception, 
            Func<TState, Exception, string> formatter)
        {
            Console.WriteLine(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= Level;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return Substitute.For<IDisposable>();
        }
    }
}