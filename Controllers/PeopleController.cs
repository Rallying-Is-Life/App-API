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
        private static string ParseFlagCode(string country, string region)
        {
            if (string.IsNullOrEmpty(country))
                return string.Empty;
            if (country.Equals("GB", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(region))
                    return "gb";
                switch (region.ToLower())
                {
                    case "wales": return "gb-wls";
                    case "scotland": return "gb-sct";
                    case "england": return "gb-eng";
                    case "northern ireland": case "ni": return "gb-nir";
                    default: return "gb";
                }
            }
            return country.ToLower();
        }

        [HttpGet] public async Task<IActionResult> Get([FromQuery] string type = null, [FromQuery] string role = null)
        {
            var q = (type ?? role ?? "").ToLower(); // directory search enabled
            var filterRole = q.Contains("co") || q.Contains("codriver") ? "CoDriver" : "Driver";
            var u = Environment.GetEnvironmentVariable("azureStorageUri")!;
            var cred = new DefaultAzureCredential();
            var idx = new TableServiceClient(new Uri(u), cred).GetTableClient(TableNames.tblPersonEventIndex);

            // Group by personRowKey (PartitionKey) and count events
            var personEventCounts = new Dictionary<string, int>();
            await foreach (var e in idx.QueryAsync<TableEntity>(filter: $"Role eq '{filterRole}'"))
            {
                var personRowKey = e.PartitionKey;
                if (personEventCounts.ContainsKey(personRowKey))
                    personEventCounts[personRowKey]++;
                else
                    personEventCounts[personRowKey] = 1;
            }

            var people = new List<object>();
            var peopleClient = new TableServiceClient(new Uri(u), cred).GetTableClient(TableNames.tblPeople);
            foreach (var kvp in personEventCounts)
            {
                var personRowKey = kvp.Key;
                var eventCount = kvp.Value;
                string first = "", surname = "", country = "", region = "";
                await foreach (var p in peopleClient.QueryAsync<TableEntity>(filter: $"RowKey eq '{personRowKey}'"))
                { first = p["FirstName"]?.ToString() ?? ""; surname = p["Surname"]?.ToString() ?? ""; country = p["Country"]?.ToString() ?? ""; region = p["Region"]?.ToString() ?? ""; }
                string flagCode = ParseFlagCode(country, region);
                string flag = string.IsNullOrEmpty(flagCode) ? "" : $"/images/flags/4x3/{flagCode}.svg";
                people.Add(new { personRowKey, firstName = first, surname, country = country, region = region, role = filterRole, eventCount = eventCount, flag = flag });
            }

            return Ok(new { query = type ?? role, filterRole, count = people.Count, items = people });
        }
    }
}