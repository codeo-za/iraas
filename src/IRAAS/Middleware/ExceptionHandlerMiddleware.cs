using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using IRAAS.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRAAS.Middleware
{
    public abstract class ExceptionHandlerMiddleware<T>
        : IMiddleware
        where T : Exception
    {
        private readonly Func<T, HttpContext, string> _errorMessageGenerator;
        private readonly IAppSettings _appSettings;
        private readonly Func<T, int> _errorCodeGenerator;

        public LogLevel LogLevel { get; }

        public ExceptionHandlerMiddleware(
            int errorCode,
            string errorMessage,
            IAppSettings appSettings
        ) : this(errorCode, (e, c) => errorMessage, appSettings)
        {
        }


        public ExceptionHandlerMiddleware(
            int errorCode,
            Func<T, HttpContext, string> errorMessageGenerator,
            IAppSettings appSettings
        ) : this(errorCode, errorMessageGenerator, appSettings, LogLevel.Error)
        {
        }

        public ExceptionHandlerMiddleware(
            int errorCode,
            Func<T, HttpContext, string> errorMessageGenerator,
            IAppSettings appSettings,
            LogLevel logLevel
        ) : this(_ => errorCode, errorMessageGenerator, appSettings, logLevel)
        {
        }

        public ExceptionHandlerMiddleware(
            Func<T, HttpStatusCode> statusCodeGenerator,
            Func<T, HttpContext, string> errorMessageGenerator,
            IAppSettings appSettings
        ) : this(
            e => (int) statusCodeGenerator(e),
            errorMessageGenerator,
            appSettings,
            LogLevel.Error
        )
        {
        }

        public ExceptionHandlerMiddleware(
            Func<T, HttpStatusCode> statusCodeGenerator,
            Func<T, HttpContext, string> errorMessageGenerator,
            IAppSettings appSettings,
            LogLevel logLevel
        ) : this(
            e => (int) statusCodeGenerator(e),
            errorMessageGenerator,
            appSettings,
            logLevel
        )
        {
        }

        public ExceptionHandlerMiddleware(
            Func<T, int> errorCodeGenerator,
            Func<T, HttpContext, string> errorMessageGenerator,
            IAppSettings appSettings,
            LogLevel logLevel
        )
        {
            LogLevel = logLevel;
            _errorCodeGenerator = errorCodeGenerator;
            _errorMessageGenerator = errorMessageGenerator;
            _appSettings = appSettings;
        }

        public async Task InvokeAsync(
            HttpContext context,
            RequestDelegate next
        )
        {
            try
            {
                await next.Invoke(context);
            }
            catch (T ex)
            {
                context.Response.StatusCode = _errorCodeGenerator(ex);
                var content = _appSettings.SuppressErrorDiagnostics
                    ? ""
                    : _errorMessageGenerator?.Invoke(ex, context) ?? "";


                var asBytes = Encoding.UTF8.GetBytes(content);
                context.Response.Headers["Content-Type"] = "text/plain; charset=utf-8";
                await context.Response.Body.WriteAsync(asBytes, 0, asBytes.Length);
                TryLogException(context, ex);
            }
        }

        private void TryLogException(HttpContext context, T ex)
        {
            try
            {
                var services = context.RequestServices;
                var messageGenerator = services.GetService<ILogMessageGenerator>();
                var message = messageGenerator.GenerateMessageFor(ex);

                var logger = services.GetService<ILogger<T>>();
                logger.Log(
                    LogLevel,
                    message,
                    ex
                );
            }
            catch
            {
                /* intentionally left blank: try to log but don't cry if we can't */
            }
        }
    }
}