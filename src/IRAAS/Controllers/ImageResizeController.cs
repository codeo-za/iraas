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
    private readonly IWhitelist _whitelist;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAppSettings _appSettings;

    public ImageResizeController(
        IImageResizer imageResizer,
        IImageMimeTypeProvider mimeTypeProvider,
        IWhitelist whitelist,
        IHttpContextAccessor httpContextAccessor,
        IAppSettings appSettings
    )
    {
        _imageResizer = imageResizer;
        _mimeTypeProvider = mimeTypeProvider;
        _whitelist = whitelist;
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

    // Experimental code ahead!
    // TODO: add appsetting to enable POST resizing (defaulted false)
    // TODO: add appsetting to hold a list of tokens which are authorised to POST
    // TODO: add precursor to block the request if it doesn't contain an auth header
    //       with a known token
    [Route("")]
    [HttpPost]
    public async Task<FileStreamResult> ResizeByImageData(
        [FromBody] ImageDataResizeParameters resizeParameters
    )
    {
        VerifyCanAcceptPostRequest();
        ArgumentNullException.ThrowIfNull(resizeParameters);
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

    // Middleware converts NotImplementedExceptions to 404s
    private void VerifyCanAcceptPostRequest()
    {
        if (!_appSettings.AllowPostRequests)
        {
            throw new NotImplementedException();
        }
    }
}