using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;

namespace App_API.Controllers
{
    public class PersonProfileController : ControllerBase
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
                    case "northern ireland":
                    case "ni": return "gb-nir";
                    default: return "gb";
                }
            }
            return country.ToLower();
        }

        [HttpGet("/profiles/person")]
        [AllowAnonymous]
        public async Task<ActionResult> Get([FromQuery] string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest("id parameter is required");

            var uri = Environment.GetEnvironmentVariable("azureStorageUri")!;
            var cred = new DefaultAzureCredential();

            // Get person details
            var peopleClient = new TableServiceClient(new Uri(uri), cred).GetTableClient(TableNames.tblPeople);
            string firstName = "", surname = "", country = "", region = "";
            var personEntity = await peopleClient.GetEntityIfExistsAsync<TableEntity>("person", id);
            if (!personEntity.HasValue)
                return NotFound();

            firstName = personEntity.Value.GetString("FirstName") ?? "";
            surname = personEntity.Value.GetString("Surname") ?? "";
            country = personEntity.Value.GetString("Country") ?? "";
            region = personEntity.Value.GetString("Region") ?? "";

            var displayNat = !string.IsNullOrEmpty(region) ? region : country;
            var flagCode = ParseFlagCode(country, region);
            var flagUrl = string.IsNullOrEmpty(flagCode) ? "" : $"/images/flags/4x3/{flagCode}.svg";

            // Get person's events from index
            var indexClient = new TableServiceClient(new Uri(uri), cred).GetTableClient(TableNames.tblPersonEventIndex);
            var events = new List<object>();

            await foreach (var idx in indexClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}'"))
            {
                var eventRowKey = idx.RowKey ?? "";
                var role = idx.GetString("Role") ?? "";
                var eventYear = idx.GetInt32("EventYear") ?? 0;
                var eventName = idx.GetString("EventName") ?? "";

                // Get entry details for car info and partner
                var entryClient = new TableServiceClient(new Uri(uri), cred).GetTableClient(TableNames.tblEventEntries);
                string carMake = "", carModel = "", carVrm = "", partnerName = "";
                string rallyRowKey = "";

                await foreach (var entry in entryClient.QueryAsync<TableEntity>(filter: $"RowKey eq '{eventRowKey}'"))
                {
                    rallyRowKey = entry.PartitionKey ?? "";
                    carMake = entry.GetString("CarMake") ?? "";
                    carModel = entry.GetString("CarModel") ?? "";
                    carVrm = entry.GetString("Registration") ?? "";

                    if (role.Equals("Driver", StringComparison.OrdinalIgnoreCase))
                    {
                        var coDriverRowKey = entry.GetString("CoDriverPersonRowKey") ?? "";
                        if (!string.IsNullOrEmpty(coDriverRowKey))
                        {
                            var coDriverEntity = await peopleClient.GetEntityIfExistsAsync<TableEntity>("person", coDriverRowKey);
                            if (coDriverEntity.HasValue)
                            {
                                partnerName = $"{coDriverEntity.Value.GetString("FirstName") ?? ""} {coDriverEntity.Value.GetString("Surname") ?? ""}".Trim();
                            }
                        }
                    }
                    else if (role.Equals("CoDriver", StringComparison.OrdinalIgnoreCase))
                    {
                        var driverRowKey = entry.GetString("DriverPersonRowKey") ?? "";
                        if (!string.IsNullOrEmpty(driverRowKey))
                        {
                            var driverEntity = await peopleClient.GetEntityIfExistsAsync<TableEntity>("person", driverRowKey);
                            if (driverEntity.HasValue)
                            {
                                partnerName = $"{driverEntity.Value.GetString("FirstName") ?? ""} {driverEntity.Value.GetString("Surname") ?? ""}".Trim();
                            }
                        }
                    }
                    break; // Only one entry per eventRowKey
                }

                events.Add(new
                {
                    year = eventYear,
                    rallyName = eventName,
                    rallyRowKey = rallyRowKey,
                    role = role,
                    partnerName = partnerName,
                    carMake = carMake,
                    carModel = carModel,
                    carVrm = carVrm
                });
            }

            // Sort events by year descending, then rally name
            events = events.OrderByDescending(e => ((dynamic)e).year).ThenBy(e => ((dynamic)e).rallyName).ToList();

            return Ok(new
            {
                personRowKey = id,
                firstName = firstName,
                surname = surname,
                nationality = displayNat,
                flagUrl = flagUrl,
                events = events
            });
        }
    }
}