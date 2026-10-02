using IRAAS.Middleware;
using Microsoft.AspNetCore.Mvc;
namespace IRAAS.Controllers;

[Route(Routes.HEALTH)]
public class HealthController : ControllerBase
{
  [HttpGet]
  [Route("")]
  [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
  public OkResult GetHealth()
  {
    return Ok();
  }
}
