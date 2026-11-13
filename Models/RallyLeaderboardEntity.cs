using Azure;
using Azure.Data.Tables;
namespace App_API.Models;

public class RallyLeaderboardEntity : TableEntityBase
{
    public int? TotalTimeSeconds { get; set; }
    public int StagesCompleted { get; set; }
    public int TotalStages { get; set; }
    public int? OverallPosition { get; set; }
    public int? ClassPosition { get; set; }
    public string? ClassId { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? GapToOverallLeaderSeconds { get; set; }
    public int? GapToCarInFrontSeconds { get; set; }
    public int? GapToClassLeaderSeconds { get; set; }
    public int? GapToNextInClassSeconds { get; set; }
}