using System.Threading.Tasks;
using IRAAS.Exceptions;
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

    public ImageResizeController(
        IImageResizer imageResizer,
        IImageMimeTypeProvider mimeTypeProvider,
        IWhitelist whitelist,
        IHttpContextAccessor httpContextAccessor)
    {
        _imageResizer = imageResizer;
        _mimeTypeProvider = mimeTypeProvider;
        _whitelist = whitelist;
        _httpContextAccessor = httpContextAccessor;
    }

    [Route("")]
    [HttpGet]
    public async Task<FileStreamResult> Resize(
        [FromQuery] ImageResizeOptions options = null
    )
    {
        if (!_whitelist.IsAllowed(options?.Url))
        {
            throw new ImageSourceNotAllowedException(options?.Url);
        }

        var result = await _imageResizer.Resize(
            options,
            _httpContextAccessor.HttpContext!.Request.Headers.ToDictionary()
        );
        var contentType = _mimeTypeProvider.DetermineMimeTypeFor(result.Stream);

        var headers = _httpContextAccessor.HttpContext.Response.Headers;
        result.Headers.ForEach(
            kvp => headers[kvp.Key] = kvp.Value
        );

        return new FileStreamResult(
            result.Stream,
            contentType);
    }
}