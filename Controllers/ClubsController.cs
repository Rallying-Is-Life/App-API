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
            var endpoint = Environment.GetEnvironmentVariable("TableStorageUri");
            var client = new TableClient(endpoint, TableNames.tblClubDetails);
            var list = new List<ClubTableEntity>();
            await foreach (ClubTableEntity c in client.QueryAsync<ClubTableEntity>()) { list.Add(c); }
            return list;
        }
    }
}
