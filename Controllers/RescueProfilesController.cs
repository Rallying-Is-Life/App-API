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
        var profile = new RescueRecoveryProfile { Name = id, Points = new List<StagePointLink>() };
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
            profile.Points.Add(new StagePointLink
            {
                PointType = entity.GetString("PointType") ?? "",
                StageRowKey = entity.RowKey ?? "",
                Name = stageName
            });
        }
        return Ok(profile);
    }
}
