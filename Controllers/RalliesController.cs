using Azure.Data.Tables;

namespace App_API.Controllers
{
    public class RalliesController : ControllerBase
    {
        [HttpGet("/rallies")]
        public async Task<ActionResult<List<RallyTableEntity>>> Get()
        {
            return new List<RallyTableEntity>();
        }
    }
}
