using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;

namespace App_API.Controllers
{
    public class RalliesController : ControllerBase
    {
        [HttpGet("/rallies")]
        [AllowAnonymous]
        public async Task<ActionResult<List<RallyTableEntity>>> Get(int page = 1, int size = 20)
        {
            var client = new TableClient(
                new Uri(Environment.GetEnvironmentVariable("azureStorageUri")),
                TableNames.tblRallyDetails,
                new DefaultAzureCredential());
            var list = new List<RallyTableEntity>();
            await foreach (var r in client.QueryAsync<RallyTableEntity>(maxPerPage: 100)) { list.Add(r); }
            return list.OrderByDescending(r => r.EventDate)
                       .Skip((page - 1) * size).Take(size).ToList();
        }
    }
}
