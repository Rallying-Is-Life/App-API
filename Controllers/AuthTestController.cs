using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace App_API.Controllers;
public class AuthTestController : ControllerBase
{
    [HttpGet("/auth-test")]
    [Authorize(Policy = "Read")]
    public IActionResult Get() => Ok(new { message = "auth works", user = User.Identity?.Name ?? "anonymous" });
}
