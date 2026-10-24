using System.Linq;
using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
namespace App_API.Controllers;
public class RescueProfilesController : ControllerBase
{
    [HttpGet("profiles/rescue")]
    [AllowAnonymous]
    public async Task<ActionResult<RescueRecoveryProfile>> GetRescueProfile([FromQuery] string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest("id required");
        var profile = new RescueRecoveryProfile { Name = id, Events = new List<StagePointLink>() };
        var unitClient = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            "tblRescueRecoveryUnits", new DefaultAzureCredential());
        await foreach (var r in unitClient.QueryAsync<RescueUnitEntity>(filter: $"PartitionKey eq 'rescueUnit'"))
        {
            if (r.RowKey == id) { profile.Name = r.Name ?? id; profile.Website = r.Website; break; }
        }
        var stageClient = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblRallyStages, new DefaultAzureCredential());
        var indexClient = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            "tblStageRescueRecoveryIndex", new DefaultAzureCredential());
        await foreach (var entity in indexClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}'"))
        {
            string stageName = entity.RowKey ?? "";
            await foreach (var s in stageClient.QueryAsync<TableEntity>(filter: $"RowKey eq '{entity.RowKey ?? id}'", maxPerPage: 10))
            {
                stageName = s.GetString("StageName") ?? s.RowKey ?? stageName;
            }
            // Extract rally id from stage RowKey (format: rallyid-stagenumber) to look up EventName/Year
            string stageRowKey = entity.RowKey ?? id;
            string rallyId = stageRowKey;
            int stageNum = 0;
            var parts = (entity.RowKey ?? "").Split('-');
            if (parts.Length > 1) { rallyId = string.Join("-", parts.Take(parts.Length - 1)); int.TryParse(parts.Last(), out stageNum); }
            else { int.TryParse(entity.RowKey ?? "", out stageNum); rallyId = entity.RowKey ?? ""; }
            string eventName = "";
            int year = 0;
            var rallyClient = new TableClient(
                new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
                TableNames.tblRallyDetails, new DefaultAzureCredential());
            await foreach (var r in rallyClient.QueryAsync<RallyTableEntity>(maxPerPage: 100))
            {
                if (r.RowKey == rallyId) { eventName = r.EventName; year = r.Year; break; }
            }
            profile.Events.Add(new StagePointLink
            {
                PointType = entity.GetString("PointType") ?? "",
                StageName = stageName,
                StageNumber = stageNum,
                Year = year,
                EventName = eventName
            });
        }
        profile.Events = profile.Events.OrderByDescending(e => e.Year).ToList();
        return Ok(profile);
    }
}
