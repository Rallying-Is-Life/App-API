using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Collections.Generic;
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

    [HttpGet("can-help")]
    public async Task<IActionResult> GetCanHelp()
    {
        var azureUri = Environment.GetEnvironmentVariable("azureStorageUri") ?? "";
        var credential = new DefaultAzureCredential();
        var client = new TableClient(new Uri(azureUri), TableNames.tblEventEntries, credential);

        var rallyClient = new TableClient(new Uri(azureUri), TableNames.tblRallyDetails, credential);

        var rallies = new Dictionary<string, RallyTableEntity>();
        await foreach (var r in rallyClient.QueryAsync<RallyTableEntity>(maxPerPage: 200))
        {
            if (string.IsNullOrEmpty(r.RowKey))
                continue;
            rallies[r.RowKey] = r;
        }

        var items = new List<object>();
        await foreach (var e in client.QueryAsync<TableEntity>(maxPerPage: 200))
        {
            var carRowKey = e.GetString("CarRowKey") ?? "";
            if (!string.IsNullOrEmpty(carRowKey))
                continue;

            var rallyId = e.PartitionKey ?? "";
            if (!string.IsNullOrEmpty(rallyId) && rallies.TryGetValue(rallyId, out var rally))
            {
                var year = rally.Year;
                var eventName = rally.EventName ?? "";
                var raceNumber = e.GetString("RaceNumber") ?? "";
                var carMake = e.GetString("CarMake") ?? "";
                var carModel = e.GetString("CarModel") ?? "";
                var driver = e.GetString("DriverName") ?? "";
                var coDriver = e.GetString("CoDriverName") ?? "";

                items.Add(new
                {
                    rallyRowKey = rallyId,
                    year = year,
                    eventName = eventName,
                    carNumber = raceNumber,
                    carMake = carMake,
                    carModel = carModel,
                    driver = driver,
                    coDriver = coDriver
                });
            }
        }

        var sorted = items.OrderByDescending(x =>
        {
            int y = (int?)x.GetType().GetProperty("year")?.GetValue(x) ?? 0;
            string ev = (string)x.GetType().GetProperty("eventName")?.GetValue(x) ?? "";
            string cn = (string)x.GetType().GetProperty("carNumber")?.GetValue(x) ?? "";
            return (y, ev, cn);
        }).ToList();

        return Ok(new { count = sorted.Count, items = sorted });
    }
}
