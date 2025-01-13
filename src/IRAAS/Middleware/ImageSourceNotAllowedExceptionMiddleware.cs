using IRAAS.Exceptions;

namespace IRAAS.Middleware;

public class ImageSourceNotAllowedExceptionMiddleware
    : ExceptionHandlerMiddleware<ImageSourceNotAllowedException>
{
    public ImageSourceNotAllowedExceptionMiddleware(
        IAppSettings appSettings)
        : base(403, (e, _) => $"Image source not allowed: {e?.Url}", appSettings)
    {
    }
}