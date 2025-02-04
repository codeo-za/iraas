using System;
using System.Threading.Tasks;
using IRAAS.ImageProcessing;
using Microsoft.AspNetCore.Mvc;

namespace IRAAS.Controllers;

[Route("test")]
public class TestController : Controller
{
    private readonly IAppSettings _settings;
    private readonly IUrlFetcher _fetcher;

    public TestController(
        IAppSettings settings,
        IUrlFetcher fetcher
    )
    {
        if (!settings.EnableTestPage)
        {
            throw new NotImplementedException();
        }

        _settings = settings;
        _fetcher = fetcher;
    }

    [ResponseCache(NoStore = true)]
    [Route("")]
    [HttpGet]
    public ActionResult Test()
    {
        return View(_settings);
    }

    [Route("")]
    [HttpPost]
    public async Task<long> FileSize([FromBody] FileSizeRequest req)
    {
        using var result = await _fetcher.Fetch(
            req.Url,
            Request.Headers.ToDictionary()
        );
        return result.Stream.Length;
    }

    public class FileSizeRequest
    {
        public string Url { get; set; }
    }
}