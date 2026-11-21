using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Text.Json;
using System.Xml;

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

    private TableClient GetPeopleClient() => new(
        new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
        TableNames.tblPeople,
        new DefaultAzureCredential());

    private async Task<(string Country, string Region)> GetPersonNationality(string personRowKey)
            {
                if (string.IsNullOrEmpty(personRowKey))
                    return (string.Empty, string.Empty);
           
                try
                {
                    var resp = await GetPeopleClient().GetEntityIfExistsAsync<TableEntity>("tblPeople", personRowKey);
                    if (resp.HasValue)
                    {
                        var country = resp.Value.GetString("Country") ?? string.Empty;
                        var region = resp.Value.GetString("Region") ?? string.Empty;
                        return (country, region);
                    }
                }
                catch { }
                return (string.Empty, string.Empty);
            }

            private async Task<(string Country, string Region)> GetPersonNationalityByName(string firstName, string surname)
            {
                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(surname))
                    return (string.Empty, string.Empty);
           
                try
                {
                    var filter = $"FirstName eq '{firstName}' and Surname eq '{surname}'";
                    await foreach (var entity in GetPeopleClient().QueryAsync<TableEntity>(filter: filter, maxPerPage: 5))
                    {
                        var country = entity.GetString("Country") ?? string.Empty;
                        var region = entity.GetString("Region") ?? string.Empty;
                        if (!string.IsNullOrEmpty(country))
                            return (country, region);
                    }
                }
                catch { }
                return (string.Empty, string.Empty);
            }

    private string ParseFlagCode(string country, string region)
    {
        if (string.IsNullOrEmpty(country))
            return string.Empty;
        if (country.Equals("GB", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(region))
                return "gb";
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

            // Get driver nationality from tblPeople
            var driverCountry = string.Empty;
            var driverRegion = string.Empty;
            if (!string.IsNullOrEmpty(entry?.DriverPersonRowKey))
            {
                (driverCountry, driverRegion) = await GetPersonNationality(entry.DriverPersonRowKey);
            }
            else if (!string.IsNullOrEmpty(entry?.DriverName))
            {
                var parts = entry.DriverName.Split(' ', 2);
                var first = parts.Length > 0 ? parts[0] : string.Empty;
                var last = parts.Length > 1 ? parts[1] : string.Empty;
                (driverCountry, driverRegion) = await GetPersonNationalityByName(first, last);
            }
            else if (!string.IsNullOrEmpty(entry?.DriverNat))
            {
                driverCountry = entry.DriverNat;
            }

            // Get co-driver nationality from tblPeople
            var coDriverCountry = string.Empty;
            var coDriverRegion = string.Empty;
            if (!string.IsNullOrEmpty(entry?.CoDriverPersonRowKey))
            {
                (coDriverCountry, coDriverRegion) = await GetPersonNationality(entry.CoDriverPersonRowKey);
            }
            else if (!string.IsNullOrEmpty(entry?.CoDriverName))
            {
                var parts = entry.CoDriverName.Split(' ', 2);
                var first = parts.Length > 0 ? parts[0] : string.Empty;
                var last = parts.Length > 1 ? parts[1] : string.Empty;
                (coDriverCountry, coDriverRegion) = await GetPersonNationalityByName(first, last);
            }
            else if (!string.IsNullOrEmpty(entry?.CoDriverNat))
            {
                coDriverCountry = entry.CoDriverNat;
            }

            items.Add(new
            {
                overallPosition = l.OverallPosition,
                classPosition = l.ClassPosition,
                entrantId = l.RowKey,
                driverName = entry?.DriverName ?? string.Empty,
                coDriverName = entry?.CoDriverName ?? string.Empty,
                driverNat = driverCountry,
                coDriverNat = coDriverCountry,
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
        var eeClient = GetEventEntriesClient();

        // Get stage results for this entrant (RowKey = {entrantId}_{stageSequence:D3}_{type})
        var filter = $"PartitionKey eq '{id}' and RowKey ge '{entrantId}_' and RowKey lt '{entrantId}~'";
        var stageResults = new List<StageResultEntity>();
        await foreach (var entity in srClient.QueryAsync<StageResultEntity>(filter: filter, maxPerPage: 200))
        {
            stageResults.Add(entity);
        }

        // Sort by StageSequence (numeric)
        stageResults = stageResults.OrderBy(s => s.StageSequenceInt).ToList();

        // Get rally stages for stage names
        var stagesDict = new Dictionary<int, (string StageName, string StageType, double? BogeyTimeSeconds, double? MaxTimeSeconds)>();
        await foreach (var entity in rsClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}'", maxPerPage: 200))
        {
            var seq = entity.GetInt32("StageNumber") ?? 0;
            var name = entity.GetString("StageName") ?? string.Empty;
            var surface = entity.GetString("Surface") ?? string.Empty;
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
            var bogey = bogeyTime.HasValue ? bogeyTime.Value.TotalSeconds : (double?)null;
            var max = maxTime.HasValue ? maxTime.Value.TotalSeconds : (double?)null;
            stagesDict[seq] = (name, surface, bogey, max);
        }

        // Get leaderboard summary for this entrant
        var lbResp = await lbClient.GetEntityIfExistsAsync<RallyLeaderboardEntity>(id, entrantId);
        var leaderboard = lbResp.HasValue ? lbResp.Value : null;

        // Pre-compute stage positions for all stages this entrant participated in
        var entrantStageSeqs = stageResults
            .Select(s => s.StageSequenceInt)
            .Distinct()
            .ToList();

        var stagePositions = new Dictionary<int, (int OverallPosition, int ClassPosition)>();

        foreach (var stageSeq in entrantStageSeqs)
        {
            // Get all stage results for this stage - query all and filter by StageSequenceInt in memory
            // (StageSequence in table may not be zero-padded, so D3 format may not match)
            var stageFilter = $"PartitionKey eq '{id}'";
            var allStageResults = new List<StageResultEntity>();
            await foreach (var entity in srClient.QueryAsync<StageResultEntity>(filter: stageFilter, maxPerPage: 200))
            {
                if (entity.StageSequenceInt == stageSeq)
                    allStageResults.Add(entity);
            }

            // Get entries for driver/co-driver/car info
            var allEntrantIds = allStageResults.Select(s => s.RowKey.Split('_')[0]).Distinct().ToList();
            var entries = new Dictionary<string, EventEntryTableEntity>();
            foreach (var eid in allEntrantIds)
            {
                try
                {
                    var resp = await eeClient.GetEntityIfExistsAsync<EventEntryTableEntity>(id, eid);
                    if (resp.HasValue)
                        entries[eid] = resp.Value;
                }
                catch { }
            }

            // Sort by TimeSeconds asc (null last)
            var sorted = allStageResults
                .Where(s => entries.ContainsKey(s.RowKey.Split('_')[0]))
                .OrderBy(s => s.TimeSeconds ?? int.MaxValue)
                .ToList();

            // Compute overall positions using dense ranking
            int position = 0;
            double? previousTime = null;

            // Pre-compute class position lookup
            var classPositions = new Dictionary<string, int>();
            var classGroups = sorted
                .Where(s => entries.TryGetValue(s.RowKey.Split('_')[0], out var e) && !string.IsNullOrEmpty(e.Class))
                .GroupBy(s => entries[s.RowKey.Split('_')[0]].Class)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var classGroup in classGroups)
            {
                int classPos = 0;
                double? classPrevTime = null;
                foreach (var sr in classGroup.Value)
                {
                    var eid = sr.RowKey.Split('_')[0];
                    if (sr.TimeSeconds.HasValue)
                    {
                        if (classPrevTime.HasValue && sr.TimeSeconds.Value == classPrevTime.Value)
                        {
                            // Tied - same position
                        }
                        else
                        {
                            classPos = classGroup.Value.IndexOf(sr) + 1;
                            // Adjust for ties
                            for (int i = 0; i < classGroup.Value.IndexOf(sr); i++)
                            {
                                if (classGroup.Value[i].TimeSeconds.HasValue && sr.TimeSeconds.HasValue &&
                                    classGroup.Value[i].TimeSeconds.Value == sr.TimeSeconds.Value)
                                {
                                    classPos = i + 1;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        classPos = classGroup.Value.IndexOf(sr) + 1;
                    }
                    classPositions[sr.RowKey] = classPos;
                    classPrevTime = sr.TimeSeconds;
                }
            }

            foreach (var sr in sorted)
            {
                var eid = sr.RowKey.Split('_')[0];
                if (eid == entrantId)
                {
                    // Overall position with dense ranking
                    if (sr.TimeSeconds.HasValue)
                    {
                        if (previousTime.HasValue && sr.TimeSeconds.Value == previousTime.Value)
                        {
                            // Tied - same position as previous
                        }
                        else
                        {
                            position = sorted.TakeWhile(s => s.TimeSeconds.HasValue && s.TimeSeconds.Value < sr.TimeSeconds.Value).Count() + 1;
                        }
                    }
                    else
                    {
                        position = sorted.Count(s => s.TimeSeconds.HasValue) + sorted.TakeWhile(s => !s.TimeSeconds.HasValue).TakeWhile(s => s.RowKey.Split('_')[0] != eid).Count() + 1;
                    }
                    previousTime = sr.TimeSeconds;

                    classPositions.TryGetValue(sr.RowKey, out var classPos);
                    stagePositions[stageSeq] = (position, classPos);
                    break;
                }
            }
        }

        var stages = new List<object>();
        foreach (var sr in stageResults)
        {
            stagesDict.TryGetValue(sr.StageSequenceInt, out var stageInfo);
            stagePositions.TryGetValue(sr.StageSequenceInt, out var pos);
            stages.Add(new
            {
                stageSequence = sr.StageSequenceInt,
                stageName = stageInfo.StageName,
                stageType = stageInfo.StageType,
                timeSeconds = sr.TimeSeconds,
                status = sr.Status,
                retirementReason = sr.RetirementReason,
                bogeyTimeHit = sr.BogeyTimeHit,
                maxTimeHit = sr.MaxTimeHit,
                bogeyTimeSeconds = stageInfo.BogeyTimeSeconds,
                maxTimeSeconds = stageInfo.MaxTimeSeconds,
                overallPosition = pos.OverallPosition > 0 ? pos.OverallPosition : (int?)null,
                classPosition = pos.ClassPosition > 0 ? pos.ClassPosition : (int?)null
            });
        }

        // Get event entry for this entrant to resolve nationality
        var entryResp = await eeClient.GetEntityIfExistsAsync<EventEntryTableEntity>(id, entrantId);
        var entry = entryResp.HasValue ? entryResp.Value : null;

        // Resolve driver/co-driver nationality from event entry or people table
        string driverNat = string.Empty;
        string coDriverNat = string.Empty;
        if (entry != null)
        {
            if (!string.IsNullOrEmpty(entry.DriverNat))
                driverNat = entry.DriverNat;
            else if (!string.IsNullOrEmpty(entry.DriverPersonRowKey))
            {
                var peopleClient = GetPeopleClient();
                await foreach (var person in peopleClient.QueryAsync<TableEntity>(filter: $"RowKey eq '{entry.DriverPersonRowKey}'"))
                {
                    driverNat = person.GetString("Country") ?? string.Empty;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(entry.CoDriverNat))
                coDriverNat = entry.CoDriverNat;
            else if (!string.IsNullOrEmpty(entry.CoDriverPersonRowKey))
            {
                var peopleClient = GetPeopleClient();
                await foreach (var person in peopleClient.QueryAsync<TableEntity>(filter: $"RowKey eq '{entry.CoDriverPersonRowKey}'"))
                {
                    coDriverNat = person.GetString("Country") ?? string.Empty;
                    break;
                }
            }
        }

        return Ok(new
        {
            rallyRowKey = id,
            entrantId,
            totalTimeSeconds = leaderboard?.TotalTimeSeconds,
            stagesCompleted = leaderboard?.StagesCompleted ?? 0,
            totalStages = leaderboard?.TotalStages ?? 0,
            status = leaderboard?.Status ?? string.Empty,
            driverNat = driverNat,
            coDriverNat = coDriverNat,
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

        // Get stage results for competitive stages - query all and filter by StageSequenceInt in memory
        // (StageSequence in table may not be zero-padded, so D3 format may not match)
        var filter = $"PartitionKey eq '{id}'";
        var stageResults = new List<StageResultEntity>();
        await foreach (var entity in srClient.QueryAsync<StageResultEntity>(filter: filter, maxPerPage: 200))
        {
            if (entity.StageSequenceInt == stageSeq)
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

        // Compute overall positions using dense ranking
        var items = new List<object>();
        int position = 0;
        double? previousTime = null;

        // Pre-compute class position lookup
        var classPositions = new Dictionary<string, int>();
        var classGroups = sorted
            .Where(s => entries.TryGetValue(s.RowKey.Split('_')[0], out var e) && !string.IsNullOrEmpty(e.Class))
            .GroupBy(s => entries[s.RowKey.Split('_')[0]].Class)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var classGroup in classGroups)
        {
            int classPos = 0;
            double? classPrevTime = null;
            foreach (var sr in classGroup.Value)
            {
                var eid = sr.RowKey.Split('_')[0];
                if (sr.TimeSeconds.HasValue)
                {
                    if (classPrevTime.HasValue && sr.TimeSeconds.Value == classPrevTime.Value)
                    {
                        // Tied - same position
                    }
                    else
                    {
                        classPos = classGroup.Value.IndexOf(sr) + 1;
                        // Adjust for ties
                        for (int i = 0; i < classGroup.Value.IndexOf(sr); i++)
                        {
                            if (classGroup.Value[i].TimeSeconds.HasValue && sr.TimeSeconds.HasValue &&
                                classGroup.Value[i].TimeSeconds.Value == sr.TimeSeconds.Value)
                            {
                                classPos = i + 1;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    classPos = classGroup.Value.IndexOf(sr) + 1;
                }
                classPositions[sr.RowKey] = classPos;
                classPrevTime = sr.TimeSeconds;
            }
        }

        foreach (var sr in sorted)
        {
            var eid = sr.RowKey.Split('_')[0];
            entries.TryGetValue(eid, out var entry);
            if (entry == null) continue;

            // Overall position with dense ranking
            if (sr.TimeSeconds.HasValue)
            {
                if (previousTime.HasValue && sr.TimeSeconds.Value == previousTime.Value)
                {
                    // Tied - same position as previous
                }
                else
                {
                    position = items.Count + 1;
                }
            }
            else
            {
                // No time (DNS/DNF) - position after all timed entries
                position = items.Count + 1;
            }
            previousTime = sr.TimeSeconds;

            classPositions.TryGetValue(sr.RowKey, out var classPos);

            items.Add(new
            {
                overallPosition = position,
                classPosition = classPos,
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