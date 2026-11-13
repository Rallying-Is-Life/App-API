using Azure;
using Azure.Data.Tables;
namespace App_API.Models;

public class StageResultEntity : TableEntityBase
{
    public string StageId { get; set; } = string.Empty;
    public int StageSequence { get; set; }
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