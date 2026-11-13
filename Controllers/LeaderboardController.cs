using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Linq;

namespace App_API.Controllers;

[Route("leaderboard")]
[AllowAnonymous]
public class LeaderboardController : ControllerBase
{
    // GET /leaderboard?id=<rallyRowKey>[&classId=<c>]
    [HttpGet]
    public async Task<IActionResult> GetLeaderboard([FromQuery] string id, [FromQuery] string? classId)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("id parameter is required");

        var storageUri = Environment.GetEnvironmentVariable("azureStorageUri");
        if (string.IsNullOrEmpty(storageUri))
            return StatusCode(500, "Storage URI not configured");

        // Query leaderboard entries for this rally
        var leaderboardClient = new TableClient(
            new Uri(storageUri),
            TableNames.tblRallyLeaderboard,
            new DefaultAzureCredential());

        var leaderboardEntries = new List<LeaderboardEntry>();
        await foreach (var entity in leaderboardClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{id}'",
            maxPerPage: 200))
        {
            leaderboardEntries.Add(new LeaderboardEntry
            {
                EntrantId = entity.RowKey ?? string.Empty,
                TotalTimeSeconds = entity.GetInt32("TotalTimeSeconds"),
                StagesCompleted = entity.GetInt32("StagesCompleted") ?? 0,
                TotalStages = entity.GetInt32("TotalStages") ?? 0,
                OverallPosition = entity.GetInt32("OverallPosition"),
                ClassPosition = entity.GetInt32("ClassPosition"),
                ClassId = entity.GetString("ClassId") ?? string.Empty,
                Status = entity.GetString("Status") ?? string.Empty,
                GapToOverallLeaderSeconds = entity.GetInt32("GapToOverallLeaderSeconds"),
                GapToCarInFrontSeconds = entity.GetInt32("GapToCarInFrontSeconds"),
                GapToClassLeaderSeconds = entity.GetInt32("GapToClassLeaderSeconds"),
                GapToNextInClassSeconds = entity.GetInt32("GapToNextInClassSeconds")
            });
        }

        if (!leaderboardEntries.Any())
            return Ok(new { rallyRowKey = id, count = 0, items = new List<object>() });

        // Filter by classId if provided
        if (!string.IsNullOrEmpty(classId))
        {
            leaderboardEntries = leaderboardEntries.Where(e => 
                e.ClassId.Equals(classId, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Get entrant details from tblEventEntries
        var entriesClient = new TableClient(
            new Uri(storageUri),
            TableNames.tblEventEntries,
            new DefaultAzureCredential());

        var entrantDetails = new Dictionary<string, EntrantDetail>();
        await foreach (var entity in entriesClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{id}'",
            maxPerPage: 200))
        {
            var entryRowKey = entity.RowKey ?? string.Empty;
            entrantDetails[entryRowKey] = new EntrantDetail
            {
                DriverName = entity.GetString("DriverName") ?? string.Empty,
                CoDriverName = entity.GetString("CoDriverName") ?? string.Empty,
                CarMake = entity.GetString("CarMake") ?? string.Empty,
                CarModel = entity.GetString("CarModel") ?? string.Empty,
                RaceNumber = entity.GetString("RaceNumber") ?? string.Empty,
                RaceClass = entity.GetString("RaceClass") ?? string.Empty
            };
        }

        // Join and build response items
        var responseItems = new List<object>();
        foreach (var lb in leaderboardEntries)
        {
            entrantDetails.TryGetValue(lb.EntrantId, out var detail);
            
            var item = new
            {
                overallPosition = lb.OverallPosition,
                classPosition = lb.ClassPosition,
                entrantId = lb.EntrantId,
                driverName = detail?.DriverName ?? string.Empty,
                coDriverName = detail?.CoDriverName ?? string.Empty,
                raceNumber = detail?.RaceNumber ?? string.Empty,
                raceClass = detail?.RaceClass ?? string.Empty,
                carMake = detail?.CarMake ?? string.Empty,
                carModel = detail?.CarModel ?? string.Empty,
                totalTimeSeconds = lb.TotalTimeSeconds,
                stagesCompleted = lb.StagesCompleted,
                totalStages = lb.TotalStages,
                status = lb.Status,
                gapToOverallLeaderSeconds = lb.GapToOverallLeaderSeconds,
                gapToCarInFrontSeconds = lb.GapToCarInFrontSeconds,
                gapToClassLeaderSeconds = lb.GapToClassLeaderSeconds,
                gapToNextInClassSeconds = lb.GapToNextInClassSeconds
            };
            responseItems.Add(item);
        }

        // Sort by OverallPosition asc (finishers first, then retirees)
        // OverallPosition is null for non-finishers; they sort after finishers
        var sortedItems = responseItems
            .OrderBy(i => 
            {
                var pos = (int?)i.GetType().GetProperty("overallPosition")?.GetValue(i);
                return pos ?? int.MaxValue;
            })
            .ThenBy(i => (int?)i.GetType().GetProperty("classPosition")?.GetValue(i) ?? int.MaxValue)
            .ToList();

        return Ok(new { rallyRowKey = id, count = sortedItems.Count, items = sortedItems });
    }

    private class LeaderboardEntry
    {
        public string EntrantId { get; set; } = string.Empty;
        public int? TotalTimeSeconds { get; set; }
        public int StagesCompleted { get; set; }
        public int TotalStages { get; set; }
        public int? OverallPosition { get; set; }
        public int? ClassPosition { get; set; }
        public string ClassId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? GapToOverallLeaderSeconds { get; set; }
        public int? GapToCarInFrontSeconds { get; set; }
        public int? GapToClassLeaderSeconds { get; set; }
        public int? GapToNextInClassSeconds { get; set; }
    }

    private class EntrantDetail
    {
        public string DriverName { get; set; } = string.Empty;
        public string CoDriverName { get; set; } = string.Empty;
        public string CarMake { get; set; } = string.Empty;
        public string CarModel { get; set; } = string.Empty;
        public string RaceNumber { get; set; } = string.Empty;
        public string RaceClass { get; set; } = string.Empty;
    }
}