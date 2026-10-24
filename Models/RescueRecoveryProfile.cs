using Azure;
using Azure.Data.Tables;
namespace App_API.Models;
public class RescueUnitEntity : TableEntityBase
{
    public string? Name { get; set; }
    public string? Website { get; set; }
}
public class RecoveryUnitEntity : TableEntityBase
{
    public string? Name { get; set; }
    public string? Website { get; set; }
}
public class StagePointLink
{
    public string PointType { get; set; } = string.Empty; // start / midpoint
    public string StageRowKey { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}