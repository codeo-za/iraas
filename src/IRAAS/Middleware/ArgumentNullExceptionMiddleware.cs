using System;
using Microsoft.AspNetCore.Http;

namespace IRAAS.Middleware;

public class ArgumentNullExceptionMiddleware
    : ExceptionHandlerMiddleware<ArgumentNullException>
{
    public ArgumentNullExceptionMiddleware(
        IAppSettings appSettings
    ) : base(
        400,
        (e, _) => e.ParamName ?? "",
        appSettings
    )
    {
    }
}