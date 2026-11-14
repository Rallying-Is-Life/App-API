using Azure;
using Azure.Data.Tables;
namespace App_API.Models;

public class EventEntryTableEntity : TableEntityBase
{
    public string DriverName { get; set; } = string.Empty;
    public string CoDriverName { get; set; } = string.Empty;
    public string DriverNat { get; set; } = string.Empty;
    public string CoDriverNat { get; set; } = string.Empty;
    public string CarMake { get; set; } = string.Empty;
    public string CarModel { get; set; } = string.Empty;
    public string VRM { get; set; } = string.Empty;
    public string RaceNumber { get; set; } = string.Empty;
    public string RaceClass { get; set; } = string.Empty;
    public int? StartingOrder { get; set; }
    public string? Class { get; set; }
}