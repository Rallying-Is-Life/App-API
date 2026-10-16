using App_API.Models;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;

namespace App_API.Controllers
{
    public class RalliesController : ControllerBase
    {
        [HttpGet("/rallies")]
        public async Task<ActionResult<List<RallyTableEntity>>> Get()
        {
            var client = new TableClient(
                new Uri(Environment.GetEnvironmentVariable("azureStorageUri")),
                TableNames.tblRallyDetails,
                new DefaultAzureCredential());
            var list = new List<RallyTableEntity>();
            await foreach (RallyTableEntity r in client.QueryAsync<RallyTableEntity>()) { list.Add(r); }
            return list;
        }
    }
}
