using System.Threading.Tasks;
using IRAAS.ImageProcessing;
using IRAAS.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace IRAAS.Controllers;

[Route("")]
public class TestController : Controller
{
    private readonly IAppSettings _settings;
    private readonly IUrlFetcher _fetcher;

    public TestController(
        IAppSettings settings,
        IUrlFetcher fetcher
    )
    {
        _settings = settings;
        _fetcher = fetcher;
    }

    [ResponseCache(NoStore = true)]
    [Route(Routes.TEST)]
    [HttpGet]
    public ActionResult Test()
    {
        return View(_settings);
    }

    [Route(Routes.SIZE)]
    [HttpGet]
    public async Task<long> FileSize([FromQuery] string url)
    {
        using var result = await _fetcher.Fetch(
            url,
            Request.Headers.ToDictionary()
        );
        return result.Stream.Length;
    }
}