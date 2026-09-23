using System.Net;
using Microsoft.AspNetCore.Http;

namespace IRAAS.Middleware;

public class BadHttpRequestExceptionMiddleware
    : ExceptionHandlerMiddleware<BadHttpRequestException>
{
    public BadHttpRequestExceptionMiddleware(
        IAppSettings appSettings
    ) : base(
        e => (HttpStatusCode)e.StatusCode,
        (e, _) => e.Message,
        appSettings
    )
    {
    }
}