using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace IRAAS.Controllers
{
    [Route("config")]
    public class ConfigController : Controller
    {
        private readonly IAppSettings _config;

        public ConfigController(IAppSettings config)
        {
            _config = config;
        }

        private static readonly JsonSerializerOptions SerializerOptions =
            new()
            {
                WriteIndented = true
            };

        [HttpGet]
        [Route("")]
        public string DumpConfig()
        {
            return JsonSerializer.Serialize(_config, SerializerOptions);
        }
    }
}