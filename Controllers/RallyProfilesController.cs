using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Linq;

namespace App_API.Controllers;

public class RallyProfilesController : ControllerBase
{
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
                var stageCount = await GetStageCount();
                return Ok(new RallyProfileSummary
                {
                    RallyName = r.EventName,
                    EventDate = r.EventDate,
                    LengthKm = r.CompetitiveDistanceKm,
                    StageCount = stageCount,
                    RallyRowKey = r.RowKey
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
        await foreach (var entity in client.QueryAsync<StageEntity>(maxPerPage: 100))
        {
            stages.Add(new StageInfo
            {
                StageNumber = entity.StageNumber,
                StageName = entity.StageName,
                StageLength = entity.StageLengthKm
            });
        }

        return Ok(stages.OrderBy(s => s.StageNumber).ToList());
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

public class RallyProfileSummary
{
    public string RallyName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public double LengthKm { get; set; }
    public int StageCount { get; set; }
    public string RallyRowKey { get; set; } = string.Empty;
}

public class StageInfo
{
    public int StageNumber { get; set; }
    public string StageName { get; set; } = string.Empty;
    public double StageLength { get; set; }
}

public class StageEntity : TableEntityBase
{
    public string StageName { get; set; } = string.Empty;
    public double StageLengthKm { get; set; }
    public int StageNumber { get; set; }
}
