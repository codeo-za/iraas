using IRAAS.ImageProcessing;
using Microsoft.Extensions.Logging;

namespace IRAAS.Middleware;

public class NotModifiedExceptionMiddleware
    : ExceptionHandlerMiddleware<NotModifiedException>
{
    public NotModifiedExceptionMiddleware(
        IAppSettings appSettings
    ) : base(304, (_, _) => null, appSettings, LogLevel.Information)
    {
    }
}