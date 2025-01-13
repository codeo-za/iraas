using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IRAAS.Middleware;

public class ProductionFallbackExceptionHandlerMiddleware
    : IMiddleware
{
    private readonly ILogger<ProductionFallbackExceptionHandlerMiddleware> _logger;

    public ProductionFallbackExceptionHandlerMiddleware(
        ILogger<ProductionFallbackExceptionHandlerMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next.Invoke(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                $"Unhandled exception servicing:\n{context.Request.QueryString}\n{ex.Message}\n{ex.StackTrace}"
            );
            context.Response.StatusCode = 500;
        }
    }
}