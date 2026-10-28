using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
namespace App_API.Controllers
{
    [AllowAnonymous]
    [Route("people")]
    public class PeopleController : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string type)
        {
            var role = (type ?? "").ToLower() == "co-driver" ? "CoDriver" : "Driver";
            var results = new List<object>();
            var u = Environment.GetEnvironmentVariable("azureStorageUri")!;
            var cred = new DefaultAzureCredential();
            var idxClient = new TableClient(new Uri(u), TableNames.tblPersonEventIndex, cred);
            await foreach (var e in idxClient.QueryAsync<TableEntity>(filter: $"Role eq '{role}'"))
            {
                results.Add(new
                {
                    rowKey = e.PartitionKey,
                    eventRowKey = e.RowKey,
                    role = e["Role"]?.ToString(),
                    eventYear = e["EventYear"]?.ToString(),
                    eventName = e["EventName"]?.ToString()
                });
            }
            return Ok(new { type, role, count = results.Count, items = results });
        }
    }
}
