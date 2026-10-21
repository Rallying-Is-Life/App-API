using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;

namespace App_API.Controllers
{
    public class ClubsController : ControllerBase
    {
        [HttpGet("/clubs")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ClubTableEntity>>> Get()
        {
            var client = new TableClient(
                new Uri(Environment.GetEnvironmentVariable("azureStorageUri")),
                TableNames.tblClubDetails,
                new DefaultAzureCredential());
            var list = new List<ClubTableEntity>();
            await foreach (ClubTableEntity c in client.QueryAsync<ClubTableEntity>()) { list.Add(c); }
            return list;
        }
    }
}
