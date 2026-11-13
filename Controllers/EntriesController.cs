using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Linq;

namespace App_API.Controllers;

[Route("entries")]
[AllowAnonymous]
public class EntriesController : ControllerBase
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

    private static string BuildCarLogoUrl(string make)
    {
        if (string.IsNullOrEmpty(make))
            return string.Empty;
        var storageAccount = Environment.GetEnvironmentVariable("StorageAccountName") ?? "rallyingislife";
        return $"https://{storageAccount}.blob.core.windows.net/mfrlogo/{make.ToLowerInvariant()}.png";
    }

    // GET /entries/unseeded?id=<rallyRowKey> — alphabetical by driver name
    [HttpGet("unseeded")]
    public async Task<IActionResult> GetUnseeded([FromQuery] string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("id parameter is required");

        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblEventEntries,
            new DefaultAzureCredential());

        var entries = new List<object>();
        await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}'", maxPerPage: 200))
        {
            var driverName = entity.GetString("DriverName") ?? "";
            var coDriverName = entity.GetString("CoDriverName") ?? "";
            var carMake = entity.GetString("CarMake") ?? "";
            var carModel = entity.GetString("CarModel") ?? "";
            var vrm = entity.GetString("VRM") ?? "";
            var raceNumber = entity.GetString("RaceNumber") ?? "";
            var raceClass = entity.GetString("RaceClass") ?? "";
            var startingOrder = entity.GetInt32("StartingOrder");

            entries.Add(new
            {
                driver = driverName,
                coDriver = coDriverName,
                car = new
                {
                    logoUrl = BuildCarLogoUrl(carMake),
                    make = carMake,
                    model = carModel
                },
                registration = vrm,
                raceClass = raceClass,
                startingOrder = startingOrder
            });
        }

        // Sort alphabetically by driver name
        var sorted = entries.OrderBy(e => ((string)e.GetType().GetProperty("driver")?.GetValue(e) ?? "")).ToList();

        return Ok(new { rallyRowKey = id, count = sorted.Count, items = sorted });
    }

    // GET /entries/seeded?id=<rallyRowKey> — by car number (RaceNumber)
    [HttpGet("seeded")]
    public async Task<IActionResult> GetSeeded([FromQuery] string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("id parameter is required");

        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblEventEntries,
            new DefaultAzureCredential());

        var entries = new List<object>();
        await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}'", maxPerPage: 200))
        {
            var driverName = entity.GetString("DriverName") ?? "";
            var coDriverName = entity.GetString("CoDriverName") ?? "";
            var carMake = entity.GetString("CarMake") ?? "";
            var carModel = entity.GetString("CarModel") ?? "";
            var vrm = entity.GetString("VRM") ?? "";
            var raceNumber = entity.GetString("RaceNumber") ?? "";
            var raceClass = entity.GetString("RaceClass") ?? "";
            var startingOrder = entity.GetInt32("StartingOrder");

            entries.Add(new
            {
                carNumber = raceNumber,
                driver = driverName,
                coDriver = coDriverName,
                car = new
                {
                    logoUrl = BuildCarLogoUrl(carMake),
                    make = carMake,
                    model = carModel
                },
                registration = vrm,
                raceClass = raceClass,
                startingOrder = startingOrder
            });
        }

        // Sort by RaceNumber (car number) - try numeric sort, fallback to string
        // RaceNumber is string but should be numeric; handle "1, 10, 2" issue
        var sorted = entries
            .OrderBy(e =>
            {
                var cn = (string)e.GetType().GetProperty("carNumber")?.GetValue(e) ?? "";
                return int.TryParse(cn, out var n) ? n : int.MaxValue;
            })
            .ThenBy(e => (string)e.GetType().GetProperty("carNumber")?.GetValue(e) ?? "")
            .ThenBy(e => (string)e.GetType().GetProperty("driver")?.GetValue(e) ?? "")
            .ToList();

        return Ok(new { rallyRowKey = id, count = sorted.Count, items = sorted });
    }
}
