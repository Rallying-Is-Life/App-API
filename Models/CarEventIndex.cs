using Azure.Data.Tables;
namespace App_API.Models {
public class CarEventIndex : TableEntityBase {
public string CarRowKey { get; set; } = "";
public string EventRowKey { get; set; } = "";
public DateTimeOffset? EventDate { get; set; }
public string? EntrantSponsor { get; set; }
public int Class { get; set; }
} }
