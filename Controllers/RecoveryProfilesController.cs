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
    public async Task<ActionResult<RecoveryUnitEntity>> GetRecoveryProfile([FromQuery] string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest("id required");
        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            "tblRescueRecoveryUnits", new DefaultAzureCredential());
        await foreach (var r in client.QueryAsync<RecoveryUnitEntity>(filter: "PartitionKey eq 'recoveryUnit'"))
        {
            if (r.RowKey == id) return Ok(r);
        }
        return NotFound();
    }
}