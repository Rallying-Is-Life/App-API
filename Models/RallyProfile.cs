using Azure;
using Azure.Data.Tables;
namespace App_API.Models;
public class RallyProfileSummary
{
    public string RallyName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public double LengthKm { get; set; }
    public int StageCount { get; set; }
    public string RallyRowKey { get; set; } = string.Empty;
}
public class StageInfo
{
    public int StageNumber { get; set; }
    public string StageName { get; set; } = string.Empty;
    public double StageLength { get; set; }
}
public class StageEntity : TableEntityBase
{
    public string StageName { get; set; } = string.Empty;
    public double StageLengthKm { get; set; }
    public int StageNumber { get; set; }
}