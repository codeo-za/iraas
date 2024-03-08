using System.IO;
using Microsoft.AspNetCore.Mvc;

namespace IRAAS.StressTest.ImageServer.Controllers;

[ApiController]
[Route("/")]
public class ImageController : ControllerBase
{
    [HttpGet]
    public FileResult Get(int id)
    {
        return id % 2 == 0
            ? BitmapResponse()
            : JpegResponse();
    }

    private FileResult JpegResponse()
    {
        var stream = new MemoryStream(Resources.Data.FluffyCatJpeg);
        return File(stream, "image/jpg");
    }

    private FileResult BitmapResponse()
    {
        var stream = new MemoryStream(Resources.Data.FluffyCatBmp);
        return File(stream, "image/bmp");
    }
}