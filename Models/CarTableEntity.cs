using Azure.Data.Tables;
namespace App_API.Models {
public class CarTableEntity : TableEntityBase {
public string Make { get; set; } = "";
public string Model { get; set; } = "";
public string VRM { get; set; } = "";
public string VIN { get; set; } = "";
} }
