using App_API.Models;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;

namespace App_API.Controllers
{
    public class ClubsController : ControllerBase
    {
        [HttpGet("/clubs")]
        public async Task<ActionResult<List<ClubTableEntity>>> Get()
        {
            // TODO: inject TableClient; query Table Storage
            return new List<ClubTableEntity>();
        }
    }
}
