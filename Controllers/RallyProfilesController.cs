using App_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Data.Tables;
using Azure.Identity;
using System.Linq;
using System.Xml;

namespace App_API.Controllers;

public class RallyProfilesController : ControllerBase
{
    private static string ParseFlagCode(string country, string region)
    {
        if (string.IsNullOrEmpty(country))
            return string.Empty;
        if (string.IsNullOrEmpty(region))
            return country.ToLower();
        if (country.Equals("GB", StringComparison.OrdinalIgnoreCase))
        {
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
                var entryCount = await GetEntryCount(id);
                var stageCount = await GetStageCount(id);
                return Ok(new RallyProfileSummary
                {
                    RallyName = r.EventName,
                    EventDate = r.EventDate,
                    EndDate = r.EndDate,
                    LengthKm = r.CompetitiveDistanceKm,
                    StageCount = stageCount,
                    EntryCount = entryCount,
                    RallyRowKey = r.RowKey,
                    Website = r.Website ?? string.Empty,
                    Country = r.Country,
                    Region = r.Region,
                    FlagUrl = string.IsNullOrEmpty(ParseFlagCode(r.Country, r.Region)) ? "" :
                             $"/images/flags/4x3/{ParseFlagCode(r.Country, r.Region)}.svg",
                    LogoUrl = r.LogoUrl
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
        await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}'", maxPerPage: 100))
        {
            var parts = (entity.RowKey ?? "").Split('-');
            var stageNum = int.TryParse(parts.Length > 1 ? parts.Last() : parts.FirstOrDefault(), out var n) ? n : 0;
            var country = entity.GetString("Country") ?? "";
            var region = entity.GetString("Region") ?? "";
            var flagCode = ParseFlagCode(country, region);
            var flagUrl = string.IsNullOrEmpty(flagCode) ? "" : $"/images/flags/4x3/{flagCode}.svg";
            var stageLengthKm = entity.GetDouble("StageLength") ?? 0;
            var lengthUnit = entity.GetString("LengthUnit") ?? "km";
            var stageLengthDisplay = $"{stageLengthKm} {lengthUnit}";
            // StageDate may be stored as DateTimeOffset (when set) or string (empty string when not set in Admin)
            DateTimeOffset? stageDate = null;
            if (entity.TryGetValue("StageDate", out var stageDateObj))
            {
                if (stageDateObj is DateTimeOffset dto)
                    stageDate = dto;
                else if (stageDateObj is DateTime dt)
                    stageDate = new DateTimeOffset(dt, TimeSpan.Zero);
                else if (stageDateObj is string s && DateTimeOffset.TryParse(s, out var parsed))
                    stageDate = parsed;
            }
            var leg = entity.GetString("Leg") ?? string.Empty;
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
            stages.Add(new StageInfo
            {
                StageNumber = stageNum,
                StageName = entity.GetString("StageName") ?? entity.RowKey ?? "",
                StageLength = stageLengthDisplay,
                StageType = surface,
                FlagUrl = flagUrl,
                StageDate = stageDate,
                Leg = leg,
                BogeyTimeSeconds = bogeyTime.HasValue ? bogeyTime.Value.TotalSeconds : null,
                MaxTimeSeconds = maxTime.HasValue ? maxTime.Value.TotalSeconds : null
            });
        }

        return Ok(stages.OrderBy(s => s.StageNumber).ToList());
    }

    // Single stage profile (?id=<rallyRowKey>&stageSeq=<n>) — returns stage details + rescue/recovery units + stage leaderboard
    [HttpGet("profiles/stage")]
    [AllowAnonymous]
    public async Task<ActionResult<StageProfileResponse>> GetStageProfile([FromQuery] string id, [FromQuery] int stageSeq)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("id parameter is required");
        if (stageSeq <= 0)
            return BadRequest("stageSeq parameter is required");

        // Get stage details from tblRallyStages
        var stageClient = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblRallyStages,
            new DefaultAzureCredential());

        StageInfo? stageInfo = null;
        await foreach (var entity in stageClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{id}' and StageNumber eq {stageSeq}", maxPerPage: 1))
        {
            var parts = (entity.RowKey ?? "").Split('-');
            var stageNum = int.TryParse(parts.Length > 1 ? parts.Last() : parts.FirstOrDefault(), out var n) ? n : 0;
            var country = entity.GetString("Country") ?? "";
            var region = entity.GetString("Region") ?? "";
            var flagCode = ParseFlagCode(country, region);
            var flagUrl = string.IsNullOrEmpty(flagCode) ? "" : $"/images/flags/4x3/{flagCode}.svg";
            var stageLengthKm = entity.GetDouble("StageLength") ?? 0;
            var lengthUnit = entity.GetString("LengthUnit") ?? "km";
            DateTimeOffset? stageDate = null;
            if (entity.TryGetValue("StageDate", out var stageDateObj))
            {
                if (stageDateObj is DateTimeOffset dto)
                    stageDate = dto;
                else if (stageDateObj is DateTime dt)
                    stageDate = new DateTimeOffset(dt, TimeSpan.Zero);
                else if (stageDateObj is string s && DateTimeOffset.TryParse(s, out var parsed))
                    stageDate = parsed;
            }
            var leg = entity.GetString("Leg") ?? "";
            var surface = entity.GetString("Surface") ?? "";
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

            stageInfo = new StageInfo
            {
                StageNumber = stageNum,
                StageName = entity.GetString("StageName") ?? entity.RowKey ?? "",
                StageLength = $"{stageLengthKm} {lengthUnit}",
                StageType = surface,
                FlagUrl = flagUrl,
                StageDate = stageDate,
                Leg = leg,
                BogeyTimeSeconds = bogeyTime.HasValue ? bogeyTime.Value.TotalSeconds : null,
                MaxTimeSeconds = maxTime.HasValue ? maxTime.Value.TotalSeconds : null
            };
            break;
        }

        if (stageInfo == null)
            return NotFound();

        // Get rescue/recovery units for this stage from tblStageRescueRecoveryIndex
        var indexClient = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            "tblStageRescueRecoveryIndex",
            new DefaultAzureCredential());

        var rescueUnits = new List<StageUnitLink>();
        var recoveryUnits = new List<StageUnitLink>();

        // Stage RowKey format in index is typically {rallyId}-{stageNumber}
        var stageRowKey = $"{id}-{stageSeq}";
        await foreach (var entity in indexClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{stageRowKey}'"))
        {
            var pointType = entity.GetString("PointType") ?? "";
            var unitId = entity.GetString("UnitId") ?? "";
            var unitType = entity.GetString("UnitType") ?? ""; // "rescue" or "recovery"

            // Get unit details from tblRescueRecoveryUnits
            var unitClient = new TableClient(
                new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
                "tblRescueRecoveryUnits",
                new DefaultAzureCredential());

            var partitionKey = unitType == "recovery" ? "recoveryUnit" : "rescueUnit";
            await foreach (var u in unitClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{partitionKey}' and RowKey eq '{unitId}'", maxPerPage: 1))
            {
                var link = new StageUnitLink
                {
                    UnitId = unitId,
                    Name = u.GetString("Name") ?? unitId,
                    Website = u.GetString("Website") ?? "",
                    PointType = pointType,
                    UnitType = unitType
                };

                if (unitType == "recovery")
                    recoveryUnits.Add(link);
                else
                    rescueUnits.Add(link);

                break;
            }
        }

        // Get stage leaderboard (overall + class positions) - reuse logic from LeaderboardController
        var leaderboard = await GetStageLeaderboardAsync(id, stageSeq);

        return Ok(new StageProfileResponse
        {
            Stage = stageInfo,
            RescueUnits = rescueUnits,
            RecoveryUnits = recoveryUnits,
            Leaderboard = leaderboard
        });
    }

    private async Task<List<StageLeaderboardItem>> GetStageLeaderboardAsync(string rallyId, int stageSeq)
    {
        var srClient = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblStageResults,
            new DefaultAzureCredential());

        var eeClient = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblEventEntries,
            new DefaultAzureCredential());

        // Get stage results for competitive stages - query all and filter by StageSequenceInt in memory
        // (StageSequence in table may not be zero-padded, so D3 format may not match)
        var filter = $"PartitionKey eq '{rallyId}'";
        var stageResults = new List<StageResultEntity>();
        await foreach (var entity in srClient.QueryAsync<StageResultEntity>(filter: filter, maxPerPage: 200))
        {
            if (entity.StageSequenceInt == stageSeq)
                stageResults.Add(entity);
        }

        if (stageResults.Count == 0)
            return new List<StageLeaderboardItem>();

        // Get entries for driver/co-driver/car info
        var entrantIds = stageResults.Select(s => s.RowKey.Split('_')[0]).Distinct().ToList();
        var entries = new Dictionary<string, EventEntryTableEntity>();
        foreach (var eid in entrantIds)
        {
            try
            {
                var resp = await eeClient.GetEntityIfExistsAsync<EventEntryTableEntity>(rallyId, eid);
                if (resp.HasValue)
                    entries[eid] = resp.Value;
            }
            catch { }
        }

        // Sort by TimeSeconds asc (null last)
        var sorted = stageResults
            .Where(s => entries.ContainsKey(s.RowKey.Split('_')[0]))
            .OrderBy(s => s.TimeSeconds ?? int.MaxValue)
            .ToList();

        // Compute class positions within each class (dense ranking)
        var classPositions = new Dictionary<string, int>();
        foreach (var sr in sorted)
        {
            var eid = sr.RowKey.Split('_')[0];
            if (!entries.TryGetValue(eid, out var entry) || string.IsNullOrEmpty(entry.RaceClass))
                continue;

            var classKey = $"{entry.RaceClass}|{sr.RowKey}";
            if (!classPositions.ContainsKey(classKey))
            {
                int classPos = 1;
                foreach (var other in sorted)
                {
                    var otherEid = other.RowKey.Split('_')[0];
                    if (!entries.TryGetValue(otherEid, out var otherEntry) || otherEntry.RaceClass != entry.RaceClass)
                        continue;
                    if (other.TimeSeconds.HasValue && sr.TimeSeconds.HasValue && other.TimeSeconds.Value < sr.TimeSeconds.Value)
                        classPos++;
                    else if (other.TimeSeconds.HasValue && sr.TimeSeconds.HasValue && other.TimeSeconds.Value == sr.TimeSeconds.Value && other.RowKey.CompareTo(sr.RowKey) < 0)
                        classPos++;
                }
                classPositions[classKey] = classPos;
            }
        }

        // Compute overall positions (dense ranking)
        var items = new List<StageLeaderboardItem>();
        int position = 0;
        double? previousTime = null;

        foreach (var sr in sorted)
        {
            var eid = sr.RowKey.Split('_')[0];
            entries.TryGetValue(eid, out var entry);
            if (entry == null) continue;

            // Overall position
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

            var classKey = entry.RaceClass != null ? $"{entry.RaceClass}|{sr.RowKey}" : "";
            classPositions.TryGetValue(classKey, out var classPosition);

            items.Add(new StageLeaderboardItem
            {
                OverallPosition = position,
                ClassPosition = classPosition > 0 ? classPosition : 0,
                EntrantId = eid,
                DriverName = entry.DriverName,
                CoDriverName = entry.CoDriverName,
                RaceNumber = entry.RaceNumber,
                RaceClass = entry.RaceClass,
                TimeSeconds = sr.TimeSeconds,
                Status = sr.Status
            });
        }

        return items;
    }

    private static async Task<int> GetEntryCount(string rallyRowKey)
    {
        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblEventEntries,
            new DefaultAzureCredential());
        var count = 0;
        await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{rallyRowKey}'", maxPerPage: 100))
        {
            count++;
        }
        return count;
    }

    private static async Task<int> GetStageCount(string rallyRowKey)
    {
        var client = new TableClient(
            new Uri(Environment.GetEnvironmentVariable("azureStorageUri")!),
            TableNames.tblRallyStages,
            new DefaultAzureCredential());
        var count = 0;
        await foreach (var entity in client.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{rallyRowKey}'", maxPerPage: 100))
        {
            count++;
        }
        return count;
    }
}

// Response models for Stage Profile endpoint
public class StageProfileResponse
{
    public StageInfo Stage { get; set; } = new();
    public List<StageUnitLink> RescueUnits { get; set; } = new();
    public List<StageUnitLink> RecoveryUnits { get; set; } = new();
    public List<StageLeaderboardItem> Leaderboard { get; set; } = new();
}

public class StageUnitLink
{
    public string UnitId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string PointType { get; set; } = string.Empty;
    public string UnitType { get; set; } = string.Empty; // "rescue" or "recovery"
}

public class StageLeaderboardItem
{
    public int OverallPosition { get; set; }
    public int ClassPosition { get; set; }
    public string EntrantId { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string CoDriverName { get; set; } = string.Empty;
    public string RaceNumber { get; set; } = string.Empty;
    public string RaceClass { get; set; } = string.Empty;
    public double? TimeSeconds { get; set; }
    public string Status { get; set; } = string.Empty;
}