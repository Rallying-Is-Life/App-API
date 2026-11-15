using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;
namespace App_API.Models;

public class StageResultEntity : TableEntityBase
{
    public string StageId { get; set; } = string.Empty;
    // Table stores StageSequence as string (e.g. "001"); bind to this name, expose int for API
    public string StageSequence { get; set; } = string.Empty;
    
    [JsonIgnore]
    public int StageSequenceInt => int.TryParse(StageSequence, out var seq) ? seq : 0;
    
    public string StageType { get; set; } = string.Empty;
    public int? TimeSeconds { get; set; }
    public int StagePenaltySeconds { get; set; }
    public int RoadPenaltySeconds { get; set; }
    public int ServicePenaltySeconds { get; set; }
    public int TotalPenaltySeconds { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RetirementReason { get; set; }
    public string? RetirementDetail { get; set; }
    public bool IsSuperRallyRestart { get; set; }
    public int? OriginalStageSequence { get; set; }
    public string? ChampionshipIds { get; set; }
    public bool? BogeyTimeHit { get; set; }
    public bool? MaxTimeHit { get; set; }
}