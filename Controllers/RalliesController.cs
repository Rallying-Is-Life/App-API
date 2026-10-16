using App_API.Models;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;

namespace App_API.Controllers
{
    public class RalliesController : ControllerBase
    {
        [HttpGet("/rallies")]
        public async Task<ActionResult<List<RallyTableEntity>>> Get()
        {
            var endpoint = Environment.GetEnvironmentVariable("TableStorageUri");
            var client = new TableClient(endpoint, TableNames.tblRallyDetails);
            var list = new List<RallyTableEntity>();
            await foreach (RallyTableEntity r in client.QueryAsync<RallyTableEntity>()) { list.Add(r); }
            return list;
        }
    }
}
