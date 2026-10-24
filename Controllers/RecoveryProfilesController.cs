using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
namespace App_API.Controllers;
public class RecoveryProfilesController : ControllerBase
{
    [HttpGet("profiles/recovery")]
    [AllowAnonymous]
    public async Task<ActionResult<List<StagePointLink>>> GetRecoveryProfile([FromQuery] string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest("id required");
        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            "tblStageRescueRecoveryIndex", new DefaultAzureCredential());
        var points = new List<StagePointLink>();
        await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}'"))
        {
            points.Add(new StagePointLink
            {
                PointType = entity.GetString("PointType") ?? "",
                StageRowKey = entity.RowKey ?? "",
                Description = $"Point: {entity.GetString("PointType") ?? entity.RowKey ?? "unknown"}"
            });
        }
        return Ok(points);
    }
}
