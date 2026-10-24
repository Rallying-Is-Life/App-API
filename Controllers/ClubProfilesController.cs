using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Linq;
namespace App_API.Controllers;
public class ClubProfilesController : ControllerBase
{
    [HttpGet("profiles/club")]
    [AllowAnonymous]
    public async Task<ActionResult<ClubProfile>> GetClubProfile([FromQuery] string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest("id required");
        var clubClient = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblClubDetails, new DefaultAzureCredential());
        ClubTableEntity? club = null;
        await foreach (var c in clubClient.QueryAsync<ClubTableEntity>(maxPerPage: 100))
        {
            if (c.RowKey == id) { club = c; break; }
        }
        if (club == null) return NotFound();
        var indexClient = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblClubRallyIndex, new DefaultAzureCredential());
        var rallies = new List<RallyLink>();
        await foreach (var entry in indexClient.QueryAsync<TableEntity>(maxPerPage: 100))
        {
            if (entry.PartitionKey == id)
            {
                string rallyRowKey = entry.RowKey ?? id;
                string rallyName = rallyRowKey;
                var rallyClient = new TableClient(
                    new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
                    TableNames.tblRallyDetails, new DefaultAzureCredential());
                await foreach (var r in rallyClient.QueryAsync<RallyTableEntity>(maxPerPage: 100))
                {
                    if (r.RowKey == rallyRowKey) { rallyName = r.EventName; break; }
                }
                rallies.Add(new RallyLink
                {
                    RallyRowKey = rallyRowKey,
                    RallyName = rallyName,
                    EventDate = DateTime.Now
                });
            }
        }
        return Ok(new ClubProfile
        {
            ClubName = club.ClubName,
            Website = club.Website,
            LogoUrl = club.LogoUrl,
            Rallies = rallies.OrderByDescending(r => r.EventDate).ToList()
        });
    }
}
