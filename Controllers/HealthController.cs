using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App_API.Controllers
{
    public class HealthController : ControllerBase
    {
        [HttpGet("/health")]
        [AllowAnonymous]
        public ActionResult<object> Get() => new { status = "healthy", service = "App-API" };
    }
}
