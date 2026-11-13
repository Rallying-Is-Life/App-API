using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Text.Json;

namespace App_API.Controllers;

[Route("leaderboard")]
[AllowAnonymous]
public class LeaderboardController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private TableClient GetLeaderboardClient() => new(
        new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
        TableNames.tblRallyLeaderboard,
        new DefaultAzureCredential());

    private TableClient GetStageResultsClient() => new(
        new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
        TableNames.tblStageResults,
        new DefaultAzureCredential());

    private TableClient GetEventEntriesClient() => new(
        new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
        TableNames.tblEventEntries,
        new DefaultAzureCredential());

    private TableClient GetRallyStagesClient() => new(
        new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
        TableNames.tblRallyStages,
        new DefaultAzureCredential());

    // GET /leaderboard?id=<rallyRowKey>[&classId=<classId>]
    [HttpGet]
    public async Task<IActionResult> GetLeaderboard([FromQuery] string id, [FromQuery] string? classId)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("id parameter is required");

        var lbClient = GetLeaderboardClient();
        var eeClient = GetEventEntriesClient();

        var filter = $"PartitionKey eq '{id}'";
        if (!string.IsNullOrEmpty(classId))
            filter += $" and ClassId eq '{classId}'";

        var leaderboard = new List<RallyLeaderboardEntity>();
        await foreach (var entity in lbClient.QueryAsync<RallyLeaderboardEntity>(filter: filter, maxPerPage: 200))
        {
            leaderboard.Add(entity);
        }

        if (leaderboard.Count == 0)
            return Ok(new { rallyRowKey = id, count = 0, items = Array.Empty<object>() });

        var entrantIds = leaderboard.Select(l => l.RowKey).ToList();
        var entries = new Dictionary<string, EventEntryTableEntity>();
        foreach (var eid in entrantIds)
        {
            try
            {
                var resp = await eeClient.GetEntityIfExistsAsync<EventEntryTableEntity>(id, eid);
                if (resp.HasValue)
                    entries[eid] = resp.Value;
            }
            catch { }
        }

        // Sort: finishers first (StagesCompleted == TotalStages) by OverallPosition asc,
        // then retirees by StagesCompleted desc, TotalTimeSeconds asc
        var sorted = leaderboard
            .OrderBy(l => l.StagesCompleted == l.TotalStages ? 0 : 1)
            .ThenBy(l => l.OverallPosition ?? int.MaxValue)
            .ThenByDescending(l => l.StagesCompleted)
            .ThenBy(l => l.TotalTimeSeconds ?? int.MaxValue)
            .ToList();

        var items = new List<object>();
        foreach (var l in sorted)
        {
            entries.TryGetValue(l.RowKey, out var entry);
            items.Add(new
            {
                overallPosition = l.OverallPosition,
                classPosition = l.ClassPosition,
                entrantId = l.RowKey,
                driverName = entry?.DriverName ?? string.Empty,
                coDriverName = entry?.CoDriverName ?? string.Empty,
                raceNumber = entry?.RaceNumber ?? string.Empty,
                raceClass = entry?.RaceClass ?? string.Empty,
                carMake = entry?.CarMake ?? string.Empty,
                carModel = entry?.CarModel ?? string.Empty,
                totalTimeSeconds = l.TotalTimeSeconds,
                stagesCompleted = l.StagesCompleted,
                totalStages = l.TotalStages,
                status = l.Status,
                gapToOverallLeaderSeconds = l.GapToOverallLeaderSeconds,
                gapToCarInFrontSeconds = l.GapToCarInFrontSeconds,
                gapToClassLeaderSeconds = l.GapToClassLeaderSeconds,
                gapToNextInClassSeconds = l.GapToNextInClassSeconds
            });
        }

        return Ok(new { rallyRowKey = id, count = items.Count, items });
    }

    // GET /leaderboard/entrant?id=<rallyRowKey>&entrantId=<e>
    [HttpGet("entrant")]
    public async Task<IActionResult> GetEntrantDetail([FromQuery] string id, [FromQuery] string entrantId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(entrantId))
            return BadRequest("id and entrantId parameters are required");

        var srClient = GetStageResultsClient();
        var rsClient = GetRallyStagesClient();
        var lbClient = GetLeaderboardClient();

        // Get stage results for this entrant (RowKey = {entrantId}_{stageSequence:D3}_{type})
        var filter = $"PartitionKey eq '{id}' and RowKey ge '{entrantId}_' and RowKey lt '{entrantId}~'";
        var stageResults = new List<StageResultEntity>();
        await foreach (var entity in srClient.QueryAsync<StageResultEntity>(filter: filter, maxPerPage: 200))
        {
            stageResults.Add(entity);
        }

        // Sort by StageSequence (numeric, stored as zero-padded string)
        stageResults = stageResults
            .OrderBy(s => int.TryParse(s.StageSequence, out var seq) ? seq : 0)
            .ToList();

        // Get rally stages for stage names
        var stagesDict = new Dictionary<int, (string StageName, string StageType)>();
        await foreach (var entity in rsClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}'", maxPerPage: 200))
        {
            var seq = entity.GetInt32("StageNumber") ?? 0;
            var name = entity.GetString("StageName") ?? string.Empty;
            var type = entity.GetString("StageType") ?? string.Empty;
            stagesDict[seq] = (name, type);
        }

        // Get leaderboard summary for this entrant
        var lbResp = await lbClient.GetEntityIfExistsAsync<RallyLeaderboardEntity>(id, entrantId);
        var leaderboard = lbResp.HasValue ? lbResp.Value : null;

        var stages = new List<object>();
        foreach (var sr in stageResults)
        {
            int.TryParse(sr.StageSequence, out var seq);
            stagesDict.TryGetValue(seq, out var stageInfo);
            stages.Add(new
            {
                stageSequence = seq,
                stageName = stageInfo.StageName,
                stageType = stageInfo.StageType,
                timeSeconds = sr.TimeSeconds,
                status = sr.Status,
                retirementReason = sr.RetirementReason,
                bogeyTimeHit = sr.BogeyTimeHit,
                maxTimeHit = sr.MaxTimeHit
            });
        }

        return Ok(new
        {
            rallyRowKey = id,
            entrantId,
            totalTimeSeconds = leaderboard?.TotalTimeSeconds,
            stagesCompleted = leaderboard?.StagesCompleted ?? 0,
            totalStages = leaderboard?.TotalStages ?? 0,
            status = leaderboard?.Status ?? string.Empty,
            stages
        });
    }

    // GET /leaderboard/stage?id=<rallyRowKey>&stageSeq=<n>[&classId=<classId>]
    [HttpGet("stage")]
    public async Task<IActionResult> GetStageResults([FromQuery] string id, [FromQuery] int stageSeq, [FromQuery] string? classId)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("id parameter is required");

        var srClient = GetStageResultsClient();
        var eeClient = GetEventEntriesClient();
        var rsClient = GetRallyStagesClient();

        // Get stage name/type from tblRallyStages
        string stageName = string.Empty;
        string stageType = string.Empty;
        await foreach (var entity in rsClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}' and StageNumber eq {stageSeq}", maxPerPage: 1))
        {
            stageName = entity.GetString("StageName") ?? string.Empty;
            stageType = entity.GetString("StageType") ?? string.Empty;
            break;
        }

        // Get stage results for SpecialStage type only (StageSequence stored as string)
        var filter = $"PartitionKey eq '{id}' and StageSequence eq '{stageSeq}' and StageType eq 'SpecialStage'";
        var stageResults = new List<StageResultEntity>();
        await foreach (var entity in srClient.QueryAsync<StageResultEntity>(filter: filter, maxPerPage: 200))
        {
            stageResults.Add(entity);
        }

        if (stageResults.Count == 0)
            return Ok(new { stageSequence = stageSeq, stageName, items = Array.Empty<object>() });

        // Get entries for driver/co-driver/car info
        var entrantIds = stageResults.Select(s => s.RowKey.Split('_')[0]).Distinct().ToList();
        var entries = new Dictionary<string, EventEntryTableEntity>();
        foreach (var eid in entrantIds)
        {
            try
            {
                var resp = await eeClient.GetEntityIfExistsAsync<EventEntryTableEntity>(id, eid);
                if (resp.HasValue)
                {
                    var entry = resp.Value;
                    if (string.IsNullOrEmpty(classId) || entry.Class == classId)
                        entries[eid] = entry;
                }
            }
            catch { }
        }

        // Sort by TimeSeconds asc (null last)
        var sorted = stageResults
            .Where(s => entries.ContainsKey(s.RowKey.Split('_')[0]))
            .OrderBy(s => s.TimeSeconds ?? int.MaxValue)
            .ToList();

        var items = new List<object>();
        int position = 0;
        foreach (var sr in sorted)
        {
            var eid = sr.RowKey.Split('_')[0];
            entries.TryGetValue(eid, out var entry);
            if (entry == null) continue;

            position++;
            items.Add(new
            {
                position,
                entrantId = eid,
                driverName = entry.DriverName,
                coDriverName = entry.CoDriverName,
                raceNumber = entry.RaceNumber,
                raceClass = entry.RaceClass,
                timeSeconds = sr.TimeSeconds,
                status = sr.Status
            });
        }

        return Ok(new { stageSequence = stageSeq, stageName, items });
    }

    // GET /leaderboard/retirements?id=<rallyRowKey>[&classId=<classId>]
    [HttpGet("retirements")]
    public async Task<IActionResult> GetRetirements([FromQuery] string id, [FromQuery] string? classId)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("id parameter is required");

        var lbClient = GetLeaderboardClient();
        var eeClient = GetEventEntriesClient();

        var filter = $"PartitionKey eq '{id}' and Status ne 'Finished'";
        var leaderboard = new List<RallyLeaderboardEntity>();
        await foreach (var entity in lbClient.QueryAsync<RallyLeaderboardEntity>(filter: filter, maxPerPage: 200))
        {
            leaderboard.Add(entity);
        }

        if (leaderboard.Count == 0)
            return Ok(new { rallyRowKey = id, count = 0, items = Array.Empty<object>() });

        var entrantIds = leaderboard.Select(l => l.RowKey).ToList();
        var entries = new Dictionary<string, EventEntryTableEntity>();
        foreach (var eid in entrantIds)
        {
            try
            {
                var resp = await eeClient.GetEntityIfExistsAsync<EventEntryTableEntity>(id, eid);
                if (resp.HasValue)
                {
                    var entry = resp.Value;
                    if (string.IsNullOrEmpty(classId) || entry.Class == classId)
                        entries[eid] = entry;
                }
            }
            catch { }
        }

        // Sort: StagesCompleted desc, then TotalTimeSeconds asc
        var sorted = leaderboard
            .Where(l => entries.ContainsKey(l.RowKey))
            .OrderByDescending(l => l.StagesCompleted)
            .ThenBy(l => l.TotalTimeSeconds ?? int.MaxValue)
            .ToList();

        var items = new List<object>();
        foreach (var l in sorted)
        {
            entries.TryGetValue(l.RowKey, out var entry);
            if (entry == null) continue;

            items.Add(new
            {
                entrantId = l.RowKey,
                driverName = entry?.DriverName ?? string.Empty,
                coDriverName = entry?.CoDriverName ?? string.Empty,
                raceNumber = entry?.RaceNumber ?? string.Empty,
                raceClass = entry?.RaceClass ?? string.Empty,
                status = l.Status,
                retirementReason = l.Status,
                stagesCompleted = l.StagesCompleted
            });
        }

        return Ok(new { rallyRowKey = id, count = items.Count, items });
    }
}