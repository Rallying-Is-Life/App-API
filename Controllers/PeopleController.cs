using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using App_API.Models;
namespace App_API.Controllers
{
    [AllowAnonymous][Route("people")]
    public class PeopleController : ControllerBase
    {
        [HttpGet] public async Task<IActionResult> Get([FromQuery] string type = null, [FromQuery] string role = null)
        {
            var q = (type ?? role ?? "").ToLower();
            var filterRole = q.Contains("co") || q.Contains("codriver") ? "CoDriver" : "Driver";
            var u = Environment.GetEnvironmentVariable("azureStorageUri")!;
            var cred = new DefaultAzureCredential();
            var idx = new TableServiceClient(new Uri(u), cred).GetTableClient(TableNames.tblPersonEventIndex);
            var people = new List<object>();
            await foreach(var e in idx.QueryAsync<TableEntity>(filter: $"Role eq '{filterRole}'"))
            {
                var personRowKey = e.PartitionKey;
                var peopleClient = new TableServiceClient(new Uri(u), cred).GetTableClient(TableNames.tblPeople);
                string first = "", surname = "", nat = "";
                await foreach(var p in peopleClient.QueryAsync<TableEntity>(filter: $"RowKey eq '{personRowKey}'"))
                { first = p["FirstName"]?.ToString() ?? ""; surname = p["Surname"]?.ToString() ?? ""; nat = p["Nationality"]?.ToString() ?? ""; }
                people.Add(new { personRowKey, firstName = first, surname, nationality = nat, role = e["Role"]?.ToString(), eventYear = e["EventYear"]?.ToString(), eventName = e["EventName"]?.ToString() });
            }
            return Ok(new { query = type ?? role, filterRole, count = people.Count, items = people });
        }
    }
}