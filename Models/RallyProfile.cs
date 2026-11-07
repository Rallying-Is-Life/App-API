using Azure;
using Azure.Data.Tables;
namespace App_API.Models;
public class RallyProfileSummary
{
    public string RallyName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public double LengthKm { get; set; }
    public int StageCount { get; set; }
    public int EntryCount { get; set; }
    public string RallyRowKey { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? FlagUrl { get; set; }
    public DateTime? EndDate { get; set; }
}
public class StageInfo
{
    public int StageNumber { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string StageLength { get; set; } = string.Empty;
    public string FlagUrl { get; set; } = string.Empty;
}
public class StageEntity : TableEntityBase
{
    public string StageName { get; set; } = string.Empty;
    public double StageLengthKm { get; set; }
    public string LengthUnit { get; set; } = string.Empty;
    public int StageNumber { get; set; }
    public string? Country { get; set; } = string.Empty;
    public string? Region { get; set; } = string.Empty;
    public string? FlagCode { get; set; } = string.Empty;
}