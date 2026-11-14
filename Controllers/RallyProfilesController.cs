using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Linq;
using System.Xml;

namespace App_API.Controllers;

public class RallyProfilesController : ControllerBase
{
    private static string ParseFlagCode(string country, string region)
    {
        if (string.IsNullOrEmpty(country))
            return string.Empty;
        if (string.IsNullOrEmpty(region))
            return country.ToLower();
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
                var stageCount = await GetStageCount(id);
                return Ok(new RallyProfileSummary
                {
                    RallyName = r.EventName,
                    EventDate = r.EventDate,
                    EndDate = r.EndDate,
                    LengthKm = r.CompetitiveDistanceKm,
                    StageCount = stageCount,
                    EntryCount = entryCount,
                    RallyRowKey = r.RowKey,
                    Website = r.Website ?? string.Empty,
                    Country = r.Country,
                    Region = r.Region,
                    FlagUrl = string.IsNullOrEmpty(ParseFlagCode(r.Country, r.Region)) ? "" :
                             $"/images/flags/4x3/{ParseFlagCode(r.Country, r.Region)}.svg",
                    LogoUrl = r.LogoUrl
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
            var parts = (entity.RowKey ?? "").Split('-');
            var stageNum = int.TryParse(parts.Length > 1 ? parts.Last() : parts.FirstOrDefault(), out var n) ? n : 0;
            var country = entity.GetString("Country") ?? "";
            var region = entity.GetString("Region") ?? "";
            var flagCode = ParseFlagCode(country, region);
            var flagUrl = string.IsNullOrEmpty(flagCode) ? "" : $"/images/flags/4x3/{flagCode}.svg";
            var stageLengthKm = entity.GetDouble("StageLength") ?? 0;
            var lengthUnit = entity.GetString("LengthUnit") ?? "km";
            var stageLengthDisplay = $"{stageLengthKm} {lengthUnit}";
            // StageDate may be stored as DateTimeOffset (when set) or string (empty string when not set in Admin)
            DateTimeOffset? stageDate = null;
            if (entity.TryGetValue("StageDate", out var stageDateObj))
            {
                if (stageDateObj is DateTimeOffset dto)
                    stageDate = dto;
                else if (stageDateObj is DateTime dt)
                    stageDate = new DateTimeOffset(dt, TimeSpan.Zero);
                else if (stageDateObj is string s && DateTimeOffset.TryParse(s, out var parsed))
                    stageDate = parsed;
            }
            var leg = entity.GetString("Leg") ?? string.Empty;
            var stageType = entity.GetString("StageType") ?? string.Empty;
            // App-Admin stores BogeyTime/MaxTime as TimeSpan (stored as ISO 8601 duration string in Table Storage)
            TimeSpan? bogeyTime = null;
            TimeSpan? maxTime = null;
            if (entity.TryGetValue("BogeyTime", out var bogeyObj))
            {
                if (bogeyObj is TimeSpan ts) bogeyTime = ts;
                else if (bogeyObj is string s)
                {
                    try { bogeyTime = XmlConvert.ToTimeSpan(s); } catch { }
                }
            }
            if (entity.TryGetValue("MaxTime", out var maxObj))
            {
                if (maxObj is TimeSpan ts) maxTime = ts;
                else if (maxObj is string s)
                {
                    try { maxTime = XmlConvert.ToTimeSpan(s); } catch { }
                }
            }
            stages.Add(new StageInfo
            {
                StageNumber = stageNum,
                StageName = entity.GetString("StageName") ?? entity.RowKey ?? "",
                StageLength = stageLengthDisplay,
                StageType = stageType,
                FlagUrl = flagUrl,
                StageDate = stageDate,
                Leg = leg,
                BogeyTimeSeconds = bogeyTime.HasValue ? (int)bogeyTime.Value.TotalSeconds : null,
                MaxTimeSeconds = maxTime.HasValue ? (int)maxTime.Value.TotalSeconds : null
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

    private static async Task<int> GetStageCount(string rallyRowKey)
    {
        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblRallyStages,
            new DefaultAzureCredential());
        var count = 0;
        await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{rallyRowKey}'", maxPerPage: 100))
        {
            count++;
        }
        return count;
    }
}