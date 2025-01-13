using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IRAAS.Middleware;

public class MaxClientsMiddleware : IMiddleware
{
    private readonly SemaphoreFacade _semaphore;

    public MaxClientsMiddleware(
        IAppSettings appSettings,
        ILogger<ConcurrencyMiddleware> logger
    )
    {
        _semaphore = new SemaphoreFacade(appSettings.MaxClients);
    }

    public async Task InvokeAsync(
        HttpContext context,
        RequestDelegate next
    )
    {
        if (!await _semaphore.WaitAsync(0))
        {
            context.Response.StatusCode = (int) HttpStatusCode.ServiceUnavailable;
            return;
        }

        try
        {
            await next.Invoke(context);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}