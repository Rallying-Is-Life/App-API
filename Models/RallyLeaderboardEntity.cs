using Azure;
using Azure.Data.Tables;
namespace App_API.Models;

public class RallyLeaderboardEntity : TableEntityBase
{
    public double? TotalTimeSeconds { get; set; }
    public int StagesCompleted { get; set; }
    public int TotalStages { get; set; }
    public int? OverallPosition { get; set; }
    public int? ClassPosition { get; set; }
    public string? ClassId { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? GapToOverallLeaderSeconds { get; set; }
    public double? GapToCarInFrontSeconds { get; set; }
    public double? GapToClassLeaderSeconds { get; set; }
    public double? GapToNextInClassSeconds { get; set; }
}