using System;

namespace IRAAS.Middleware;

public class ArgumentExceptionMiddleware
    : ExceptionHandlerMiddleware<ArgumentException>
{
    public ArgumentExceptionMiddleware(
        IAppSettings appSettings
    ) : base(
        400,
        (e, _) => e.Message,
        appSettings
    )
    {
    }
}