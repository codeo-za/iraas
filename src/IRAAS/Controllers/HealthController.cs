using IRAAS.Middleware;
using Microsoft.AspNetCore.Mvc;
namespace IRAAS.Controllers;

[Route(Routes.HEALTH)]
public class HealthController : ControllerBase
{
  [HttpGet]
  [Route("")]
  public OkResult GetHealth()
  {
    return Ok();
  }
}
