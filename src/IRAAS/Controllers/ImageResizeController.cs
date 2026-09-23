using System;
using System.Threading.Tasks;
using IRAAS.ImageProcessing;
using IRAAS.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PeanutButter.Utils;

namespace IRAAS.Controllers;

[Route("")]
public class ImageResizeController
{
    private readonly IImageResizer _imageResizer;
    private readonly IImageMimeTypeProvider _mimeTypeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAppSettings _appSettings;

    public ImageResizeController(
        IImageResizer imageResizer,
        IImageMimeTypeProvider mimeTypeProvider,
        IHttpContextAccessor httpContextAccessor,
        IAppSettings appSettings
    )
    {
        _imageResizer = imageResizer;
        _mimeTypeProvider = mimeTypeProvider;
        _httpContextAccessor = httpContextAccessor;
        _appSettings = appSettings;
    }

    [Route("")]
    [HttpGet]
    public async Task<FileStreamResult> ResizeByUrl(
        [FromQuery] ImageUrlResizeParameters resizeParameters = null
    )
    {
        var result = await _imageResizer.Resize(
            resizeParameters,
            _httpContextAccessor.HttpContext!.Request.Headers.ToDictionary()
        );
        var contentType = _mimeTypeProvider.DetermineMimeTypeFor(result.Stream);

        var headers = _httpContextAccessor.HttpContext.Response.Headers;
        result.Headers.ForEach(
            kvp => headers[kvp.Key] = kvp.Value
        );

        return new FileStreamResult(
            result.Stream,
            contentType
        );
    }

    /// <summary>
    /// this method is only reachable if allowed by AuthorizationMiddleware:
    /// - appsettings must have AllowPostRequest set to "true"
    /// - appsettings must either have tokens set in PostAuthTokens (comma-separated string)
    ///     or PostAuthTokens can equal "*" for open posting
    /// </summary>
    /// <param name="resizeParameters"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    [Route("")]
    [HttpPost]
    public async Task<FileStreamResult> ResizeByImageData(
        [FromBody] ImageDataResizeParameters resizeParameters
    )
    {
        if ((resizeParameters?.ImageData?.Length ?? 0) > _appSettings.MaxInputImageSize)
        {
            // The max input size check is done here rather than in
            // middleware because otherwise the model would be deserialized
            // twice - once in the middleware, and again in asp.net model-binding
            // to get the model here.
            throw new ArgumentException(
                "Input image size exceeds allowed size",
                nameof(
                    ImageDataResizeParameters.ImageData
                )
            );
        }
        
        // intentionally do not dispose of the result here
        // -> the stream is the bit that needs disposal
        //    and asp.net should dispose of it when finishing
        //    the request
        var result = await _imageResizer.Resize(
            resizeParameters
        );
        var contentType = _mimeTypeProvider.DetermineMimeTypeFor(
            result.Stream
        );
        return new FileStreamResult(
            result.Stream,
            contentType
        );
    }
}