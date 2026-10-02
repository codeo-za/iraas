using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace IRAAS.Middleware;

/// <summary>
/// When the test page is enabled, allows any origin (including a
/// standalone page opened from file://) to read GET resize and size
/// responses, along with their headers and timing information. Applied
/// as the response starts so that it also covers error responses and
/// overrides any CORS headers echoed back from the image source.
/// </summary>
public class TestPageCorsMiddleware : IMiddleware
{
    private readonly IAppSettings _appSettings;

    public TestPageCorsMiddleware(
        IAppSettings appSettings
    )
    {
        _appSettings = appSettings;
    }

    public Task InvokeAsync(
        HttpContext context,
        RequestDelegate next
    )
    {
        if (ShouldAllowCrossOriginReads(context.Request))
        {
            var response = context.Response;
            response.OnStarting(
                () =>
                {
                    response.Headers["Access-Control-Allow-Origin"] = "*";
                    response.Headers["Access-Control-Expose-Headers"] = "*";
                    response.Headers["Timing-Allow-Origin"] = "*";
                    return Task.CompletedTask;
                }
            );
        }

        return next(context);
    }

    private bool ShouldAllowCrossOriginReads(
        HttpRequest request
    )
    {
        return _appSettings.EnableTestPage &&
            HttpMethods.IsGet(request.Method) &&
            (Routes.HasResizeEndpointPath(request) || Routes.HasSizeEndpointPath(request));
    }
}
