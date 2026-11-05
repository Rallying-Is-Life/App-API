using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Linq;

namespace App_API.Controllers;

public class RallyProfilesController : ControllerBase
{
    private static string ParseFlagCode(string country, string region)
    {
        if (string.IsNullOrEmpty(country) || string.IsNullOrEmpty(region))
            return string.Empty;
        if (country.Equals("GB", StringComparison.OrdinalIgnoreCase))
        {
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

    // Single rally profile based on rowkey (?id=) — returns one rally only
    [HttpGet("profiles/rally")]
    [AllowAnonymous]
    public async Task<ActionResult<RallyProfileSummary>> GetRallyProfile([FromQuery] string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("id parameter is required");

        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblRallyDetails,
            new DefaultAzureCredential());

        await foreach (var r in client.QueryAsync<RallyTableEntity>(maxPerPage: 100))
        {
            if (r.RowKey == id)
            {
                var entryCount = await GetEntryCount(id);
                var stageCount = await GetStageCount();
                return Ok(new RallyProfileSummary
                {
                    RallyName = r.EventName,
                    EventDate = r.EventDate,
                    LengthKm = r.CompetitiveDistanceKm,
                    StageCount = stageCount,
                    EntryCount = entryCount,
                    RallyRowKey = r.RowKey,
                    Website = r.Website ?? string.Empty
                });
            }
        }

        return NotFound();
    }

    // Stages for a specific rally (?id=) — returns stages table
    [HttpGet("profiles/rally/stages")]
    [AllowAnonymous]
    public async Task<ActionResult<List<StageInfo>>> GetStagesForRally([FromQuery] string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("id parameter is required");

        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblRallyStages,
            new DefaultAzureCredential());

        var stages = new List<StageInfo>();
        await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}'", maxPerPage: 100))
        {
            // RowKey = rallyid-stagenumber; extract stage number
            var parts = (entity.RowKey ?? "").Split('-');
            var stageNum = int.TryParse(parts.Length > 1 ? parts.Last() : parts.FirstOrDefault(), out var n) ? n : 0;
            var country = entity.GetString("Country") ?? "";
            var region = entity.GetString("Region") ?? "";
            var flagCode = ParseFlagCode(country, region);
            var flagUrl = string.IsNullOrEmpty(flagCode) ? "" : $"/images/flags/4x3/{flagCode}.svg";
            var stageLengthKm = entity.GetDouble("StageLengthKm") ?? 0;
            var lengthUnit = entity.GetString("LengthUnit") ?? "km";
            var stageLengthDisplay = $"{stageLengthKm} {lengthUnit}";
            stages.Add(new StageInfo
            {
                StageNumber = stageNum,
                StageName = entity.GetString("StageName") ?? entity.RowKey ?? "",
                StageLength = stageLengthDisplay,
                FlagUrl = flagUrl
            });
        }

        return Ok(stages.OrderBy(s => s.StageNumber).ToList());
    }

    private static async Task<int> GetEntryCount(string rallyRowKey)
    {
        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblEventEntries,
            new DefaultAzureCredential());
        var count = 0;
        await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{rallyRowKey}'", maxPerPage: 100))
        {
            count++;
        }
        return count;
    }

    private static async Task<int> GetStageCount()
    {
        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblRallyStages,
            new DefaultAzureCredential());

        var count = 0;
        await foreach (var entity in client.QueryAsync<StageEntity>(maxPerPage: 100))
        {
            count++;
        }
        return count;
    }
}