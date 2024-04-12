using Microsoft.AspNetCore.Mvc;
namespace IRAAS.Controllers;

[Route("health")]
public class HealthController : ControllerBase
{
  [HttpGet]
  [Route("")]
  public OkResult GetHealth()
  {
    return Ok();
  }
}
