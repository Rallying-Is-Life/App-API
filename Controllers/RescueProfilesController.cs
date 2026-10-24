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
<<<<<<< HEAD
    public async Task<ActionResult<List<StagePointLink>>> GetRescueProfile([FromQuery] string id)
=======
    public async Task<ActionResult<RescueUnitEntity>> GetRescueProfile([FromQuery] string id)
>>>>>>> origin/Development
    {
        if (string.IsNullOrEmpty(id)) return BadRequest("id required");
        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
<<<<<<< HEAD
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
=======
            "tblRescueRecoveryUnits", new DefaultAzureCredential());
        await foreach (var r in client.QueryAsync<RescueUnitEntity>(filter: "PartitionKey eq 'rescueUnit'"))
        {
            if (r.RowKey == id) return Ok(r);
        }
        return NotFound();
    }
}
>>>>>>> origin/Development
