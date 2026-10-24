namespace App_API.Models;
using Azure.Data.Tables;
public class RescueRecoveryProfile
{
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public List<StagePointLink> Events { get; set; } = new();
}
public class StagePointLink
{
    public string PointType { get; set; } = string.Empty;
    public string StageRowKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
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
public class StageEventLink
{
    public string PointType { get; set; } = string.Empty;
    public string StageRowKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int StageNumber { get; set; }
    public int Year { get; set; }
    public string EventName { get; set; } = string.Empty;
}
